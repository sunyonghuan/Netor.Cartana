using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Execution;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using Netor.Cortana.Entitys;
using Netor.Cortana.Entitys.Services;

using System.Text.Json;

using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace Netor.Cortana.AI.Providers;

/// <summary>
/// 聊天历史数据持久化提供商，将对话消息保存到 SQLite 数据库。
/// 继承自 <see cref="ChatHistoryProvider"/>，在 Agent 执行完成后自动保存请求和响应消息。
/// </summary>
public sealed class ChatHistoryDataProvider(
    CortanaDbContext dbContext,
    SystemSettingsService systemSettings,
    AiProviderService providerService,
    AgentService agentService,
    AiModelService modelService,
    IAppPaths appPaths,
    ILogger<ChatHistoryDataProvider> logger) : ChatHistoryProvider
{
    internal const string NewSessionTitle = "新会话";
    private const string IsNewSessionStateKey = "isnewsession";

    /// <summary>
    /// 最大上下文长度（字符数），从数据库读取，回退默认值 9500。
    /// </summary>
    private int MaxContentLength => systemSettings.GetValue("ChatHistory.MaxContentLength", 9500);

    /// <summary>
    /// 最大保留的历史消息条数，从数据库读取，回退默认值 15。
    /// </summary>
    private int MaxContentCount => systemSettings.GetValue("ChatHistory.MaxContentCount", 15);

    private const string SummaryInstructionPrompt = """
        你是一个对话历史压缩器。请将以上所有会话内容总结为一段简洁的上下文摘要。

        要求：
        1. 保留所有关键信息：用户的目标、已做出的决策、已完成的操作、待办事项、涉及的文件路径和技术细节
        2. 保留最近 2-6 轮对话的核心内容，确保对话可以自然延续
        3. 如果会话中产生了代码变更，记录变更的文件和修改要点
        4. 如果存在未解决的问题或正在进行的任务，明确标注当前状态
        5. 删除寒暄、重复内容、中间试错过程，只保留最终结论
        6. 总结以第三人称描述，格式为："[会话摘要] ..."
        """;

    /// <summary>
    /// 创建新的聊天会话记录，初始标题固定为“新会话”。
    /// </summary>
    public Task<string> CreateNewSessionAsync(AgentSession session, AIAgent agent)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(agent);

        session.StateBag.SetValue(IsNewSessionStateKey, bool.TrueString);

        return AddOrUpdateSessionAsync(session, NewSessionTitle, agent);
    }

    /// <summary>
    /// Agent 执行完成后保存聊天历史到数据库。
    /// </summary>
    protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelName = context.Session?.StateBag.GetValue<string>("modelid") ?? "Unknown";
        var firstText = context.RequestMessages?.FirstOrDefault()?.Text ?? "";
        var sessionId = await AddOrUpdateSessionAsync(context.Session, firstText.Truncate(32), context.Agent);

        // 确保 ResponseMessages 不为 null 再进行 Select
        var messages = (context.ResponseMessages ?? [])
            .Select(t => new ChatMessageEntity
            {
                Role = t.Role.ToString(),
                Content = t.Text,
                AuthorName = GetChatRoleText(t.Role, context.Agent.Name ?? "未知"),
                CreatedAt = t.CreatedAt ?? DateTimeOffset.UtcNow,
                CreatedTimestamp = t.CreatedAt?.ToLocalTime().ToUnixTimeMilliseconds() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Id = t.MessageId ?? Guid.NewGuid().ToString("N"),
                SessionId = sessionId,
                UpdatedTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                ModelName = modelName
            });

        try
        {
            dbContext.ExecuteInTransaction(conn =>
            {
                foreach (var message in messages)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = """
                        INSERT OR REPLACE INTO ChatMessages
                            (Id, CreatedTimestamp, UpdatedTimestamp, SessionId, Role, AuthorName, Content, TokenCount, ModelName, CreatedAt)
                        VALUES
                            (@Id, @CreatedTimestamp, @UpdatedTimestamp, @SessionId, @Role, @AuthorName, @Content, @TokenCount, @ModelName, @CreatedAt)
                        """;
                    ChatMessageService.BindEntity(cmd, message);
                    cmd.ExecuteNonQuery();
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "保存聊天历史到数据库时失败。");
        }
    }

    /// <summary>
    /// 返回指定会话的聊天历史记录，供 Agent 上下文使用。
    /// </summary>
    protected override async ValueTask<IEnumerable<AIChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");

        if (context.Session is not null)
        {
            sessionId = context.Session.StateBag.GetValue<string>("sessionid") ?? sessionId;
        }

        try
        {
            var messages = dbContext.Query(
                "SELECT * FROM ChatMessages WHERE SessionId = @SessionId ORDER BY CreatedTimestamp DESC LIMIT @Limit",
                ChatMessageService.ReadEntity,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@SessionId", sessionId);
                    cmd.Parameters.AddWithValue("@Limit", MaxContentCount);
                })
                .Select(t => new AIChatMessage
                {
                    Role = t.ToChatRole(),
                    AuthorName = t.AuthorName,
                    MessageId = t.Id,
                    CreatedAt = t.CreatedAt?.ToLocalTime(),
                    Contents = [new TextContent(t.Content)]
                })
                .OrderBy(t => t.CreatedAt)
                .AsEnumerable();

            if (messages.Sum(t => t.Text?.Length ?? 0) > MaxContentLength)
            {
                messages = await CreateSummaryAsync(sessionId, messages, cancellationToken);
            }

            return messages;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "从数据库加载聊天历史时失败。");
            return [];
        }
    }

    /// <summary>
    /// 汇总对话历史记录，返回一条简短的摘要消息以减少 token 消耗。
    /// </summary>
    private async Task<IEnumerable<AIChatMessage>> CreateSummaryAsync(
        string sessionId,
        IEnumerable<AIChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var defaultProvider = providerService.GetAll().FirstOrDefault(t => t.IsDefault);
            if (defaultProvider is null) return messages;

            var defaultModel = modelService.GetByProviderId(defaultProvider.Id).FirstOrDefault(t => t.IsDefault);
            if (defaultModel is null) return messages;

#pragma warning disable MAAI001
            var client = AIAgentFactory
                .CreateChatClient(defaultProvider, defaultModel.Name)
                .AsAIAgent(SummaryInstructionPrompt);
#pragma warning restore MAAI001

            var result = await client.RunAsync(messages, cancellationToken: cancellationToken);
            var summary = result?.Text ?? "";

            AIChatMessage[] summaryMessages =
            [
                new()
                {
                    Role = ChatRole.Assistant,
                    AuthorName = "AI",
                    Contents = [new TextContent(summary)],
                    CreatedAt = DateTimeOffset.UtcNow,
                    MessageId = Guid.NewGuid().ToString("N")
                }
            ];

            var defaultAgent = agentService.GetAll().FirstOrDefault(t => t.IsDefault);

            UpdateSessionHistory(
                sessionId,
                messages.Select(t => t.MessageId).ToArray(),
                summaryMessages,
                defaultAgent?.Name ?? defaultProvider.Name,
                defaultModel.Name);

            return summaryMessages;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "创建对话历史摘要时失败。");
            return messages;
        }
    }

    /// <summary>
    /// 删除旧的历史消息并写入压缩后的摘要消息。
    /// </summary>
    private void UpdateSessionHistory(
        string sessionId,
        string?[] oldIds,
        IEnumerable<AIChatMessage> chats,
        string agentName,
        string modelName)
    {
        try
        {
            var validIds = oldIds.Where(id => id is not null).ToArray();

            dbContext.ExecuteInTransaction(conn =>
            {
                // 删除旧消息
                if (validIds.Length > 0)
                {
                    using var delCmd = conn.CreateCommand();
                    var paramNames = new string[validIds.Length];
                    for (int i = 0; i < validIds.Length; i++)
                    {
                        paramNames[i] = $"@id{i}";
                        delCmd.Parameters.AddWithValue(paramNames[i], validIds[i]!);
                    }
                    delCmd.CommandText = $"DELETE FROM ChatMessages WHERE Id IN ({string.Join(',', paramNames)})";
                    delCmd.ExecuteNonQuery();
                }

                // 插入摘要消息
                foreach (var t in chats)
                {
                    var entity = new ChatMessageEntity
                    {
                        Role = t.Role.ToString(),
                        Content = t.Text,
                        AuthorName = GetChatRoleText(t.Role, agentName),
                        CreatedAt = t.CreatedAt ?? DateTimeOffset.UtcNow,
                        CreatedTimestamp = t.CreatedAt?.ToLocalTime().ToUnixTimeMilliseconds() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Id = Guid.NewGuid().ToString("N"),
                        SessionId = sessionId,
                        UpdatedTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                        ModelName = modelName
                    };

                    using var insCmd = conn.CreateCommand();
                    insCmd.CommandText = """
                        INSERT INTO ChatMessages
                            (Id, CreatedTimestamp, UpdatedTimestamp, SessionId, Role, AuthorName, Content, TokenCount, ModelName, CreatedAt)
                        VALUES
                            (@Id, @CreatedTimestamp, @UpdatedTimestamp, @SessionId, @Role, @AuthorName, @Content, @TokenCount, @ModelName, @CreatedAt)
                        """;
                    ChatMessageService.BindEntity(insCmd, entity);
                    insCmd.ExecuteNonQuery();
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新会话历史记录时失败。");
        }
    }

    /// <summary>
    /// 创建或更新聊天会话记录。
    /// </summary>
    private async Task<string> AddOrUpdateSessionAsync(AgentSession? session, string title, AIAgent agent)
    {
        var sessionId = session?.StateBag.GetValue<string>("sessionid") ?? Guid.NewGuid().ToString("N");
        JsonElement? sessionJson = null;
        var shouldUpdateNewSessionTitle = session is not null
            && string.Equals(session.StateBag.GetValue<string>(IsNewSessionStateKey), bool.TrueString, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(title)
            && !string.Equals(title, NewSessionTitle, StringComparison.Ordinal);

        if (session is not null)
        {
            sessionJson = await agent.SerializeSessionAsync(session);
        }

        try
        {
            var sessionEntity = dbContext.QueryFirstOrDefault(
                "SELECT * FROM ChatSessions WHERE Id = @Id",
                ReadSessionEntity,
                cmd => cmd.Parameters.AddWithValue("@Id", sessionId));

            sessionEntity ??= new ChatSessionEntity
            {
                Id = sessionId,
                Title = title,
                CreatedTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                RawDiscription = sessionJson?.ToString() ?? "",
                Categorize = appPaths.WorkspaceDirectory.Md5Encrypt()
            };

            if (shouldUpdateNewSessionTitle && (string.IsNullOrWhiteSpace(sessionEntity.Title) || sessionEntity.Title == NewSessionTitle))
            {
                sessionEntity.Title = title;
                session!.StateBag.SetValue(IsNewSessionStateKey, bool.FalseString);
            }

            if (sessionJson is not null)
            {
                sessionEntity.RawDiscription = sessionJson.Value.ToString();
            }

            sessionEntity.UpdatedTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            sessionEntity.LastActiveTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            sessionEntity.TotalTokenCount += 0;

            dbContext.Execute(
                """
                INSERT OR REPLACE INTO ChatSessions
                    (Id, CreatedTimestamp, UpdatedTimestamp, Categorize, Title, Summary, RawDiscription, AgentId, IsArchived, IsPinned, LastActiveTimestamp, TotalTokenCount)
                VALUES
                    (@Id, @CreatedTimestamp, @UpdatedTimestamp, @Categorize, @Title, @Summary, @RawDiscription, @AgentId, @IsArchived, @IsPinned, @LastActiveTimestamp, @TotalTokenCount)
                """,
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@Id", sessionEntity.Id);
                    cmd.Parameters.AddWithValue("@CreatedTimestamp", sessionEntity.CreatedTimestamp);
                    cmd.Parameters.AddWithValue("@UpdatedTimestamp", sessionEntity.UpdatedTimestamp);
                    cmd.Parameters.AddWithValue("@Categorize", sessionEntity.Categorize);
                    cmd.Parameters.AddWithValue("@Title", sessionEntity.Title);
                    cmd.Parameters.AddWithValue("@Summary", sessionEntity.Summary);
                    cmd.Parameters.AddWithValue("@RawDiscription", sessionEntity.RawDiscription);
                    cmd.Parameters.AddWithValue("@AgentId", sessionEntity.AgentId);
                    cmd.Parameters.AddWithValue("@IsArchived", sessionEntity.IsArchived ? 1 : 0);
                    cmd.Parameters.AddWithValue("@IsPinned", sessionEntity.IsPinned ? 1 : 0);
                    cmd.Parameters.AddWithValue("@LastActiveTimestamp", sessionEntity.LastActiveTimestamp);
                    cmd.Parameters.AddWithValue("@TotalTokenCount", sessionEntity.TotalTokenCount);
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "创建或更新会话记录时失败。");
        }

        return sessionId;
    }

    /// <summary>
    /// 将 ChatRole 转换为中文显示文本。
    /// </summary>
    private static string GetChatRoleText(ChatRole role, string agentName)
    {
        if (role == ChatRole.User) return "用户";
        if (role == ChatRole.System) return "系统";
        if (role == ChatRole.Assistant) return agentName;
        if (role == ChatRole.Tool) return "工具";

        return "未知";
    }

    /// <summary>
    /// 从 SqliteDataReader 映射 ChatSessionEntity。
    /// </summary>
    private static ChatSessionEntity ReadSessionEntity(Microsoft.Data.Sqlite.SqliteDataReader r)
    {
        return new ChatSessionEntity
        {
            Id = r.GetString(r.GetOrdinal("Id")),
            CreatedTimestamp = r.GetInt64(r.GetOrdinal("CreatedTimestamp")),
            UpdatedTimestamp = r.GetInt64(r.GetOrdinal("UpdatedTimestamp")),
            Categorize = r.GetString(r.GetOrdinal("Categorize")),
            Title = r.GetString(r.GetOrdinal("Title")),
            Summary = r.GetString(r.GetOrdinal("Summary")),
            RawDiscription = r.GetString(r.GetOrdinal("RawDiscription")),
            AgentId = r.GetString(r.GetOrdinal("AgentId")),
            IsArchived = r.GetInt64(r.GetOrdinal("IsArchived")) != 0,
            IsPinned = r.GetInt64(r.GetOrdinal("IsPinned")) != 0,
            LastActiveTimestamp = r.GetInt64(r.GetOrdinal("LastActiveTimestamp")),
            TotalTokenCount = r.GetInt32(r.GetOrdinal("TotalTokenCount"))
        };
    }
}