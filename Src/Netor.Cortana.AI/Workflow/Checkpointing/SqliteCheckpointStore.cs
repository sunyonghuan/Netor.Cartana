using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Extensions.Logging;

using Netor.Cortana.Entitys.Services;

using System.Text;
using System.Text.Json;

namespace Netor.Cortana.AI.Workflow.Checkpointing;

/// <summary>
/// 阶段 5B：基于 SQLite BLOB 的 SDK <see cref="JsonCheckpointStore"/> 实现。
///
/// 设计：
/// - <c>sessionId</c> = Workflow taskId（与现有 WorkflowExecutor 调用 <c>InProcessExecution.RunStreamingAsync(workflow, input, manager, sessionId: taskId, ct)</c> 对齐）
/// - SDK 把 <see cref="JsonElement"/> 序列化为字节数组写入 BLOB；反序列化通过 <see cref="JsonDocument.Parse(System.ReadOnlySpan{byte}, JsonDocumentOptions)"/>
/// - <see cref="WorkflowCheckpointRepository"/> 承担底层 SQLite I/O（INSERT/SELECT/LIST/DELETE）
///
/// 决策 5B-C：SQLite BLOB 同库与 OrchestrationTask 同事务边界，CASCADE 级联删除。
/// 异常容忍：上层 <c>WorkflowExecutor.StartAsync</c> 启动孤儿恢复时若反序列化失败，应 catch 后 mark failed（不让 SDK 版本升级导致老 checkpoint 拖垮整个启动）。
///
/// 详见 docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §5B.2 / Phase 2 实施计划 §3.1-3.3。
/// </summary>
public sealed class SqliteCheckpointStore(
    WorkflowCheckpointRepository repository,
    ILogger<SqliteCheckpointStore> logger) : JsonCheckpointStore
{
    private readonly WorkflowCheckpointRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly ILogger<SqliteCheckpointStore> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public override ValueTask<CheckpointInfo> CreateCheckpointAsync(string sessionId, JsonElement value, CheckpointInfo? parent = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        // 1. 生成新 CheckpointId（GUID-N，与 SDK FileSystemJsonCheckpointStore.GetUnusedCheckpointInfo 一致风格）
        var checkpointId = Guid.NewGuid().ToString("N");

        // 2. 序列化 JsonElement 为 UTF8 字节数组
        //    用 JsonElement.WriteTo(Utf8JsonWriter) 而非 JsonSerializer.SerializeToUtf8Bytes<JsonElement>：
        //    后者会触发 IL2026/IL3050 AOT 警告（携带 RequiresUnreferencedCode + RequiresDynamicCode），
        //    JsonElement.WriteTo 是 source-gen 友好路径，零反射零警告。
        byte[] payload;
        try
        {
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                value.WriteTo(writer);
            }
            payload = ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkpoint 序列化失败：sessionId={SessionId}", sessionId);
            throw;
        }

        // 3. 持久化 BLOB
        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        try
        {
            _repository.Insert(sessionId, checkpointId, payload, createdAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkpoint 写入 SQLite 失败：sessionId={SessionId}, checkpointId={CheckpointId}, bytes={Size}",
                sessionId, checkpointId, payload.Length);
            throw;
        }

        _logger.LogDebug(
            "Checkpoint 已写入：sessionId={SessionId}, checkpointId={CheckpointId}, bytes={Size}",
            sessionId, checkpointId, payload.Length);

        return new ValueTask<CheckpointInfo>(new CheckpointInfo(sessionId, checkpointId));
    }

    /// <inheritdoc/>
    public override ValueTask<JsonElement> RetrieveCheckpointAsync(string sessionId, CheckpointInfo key)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentNullException.ThrowIfNull(key);

        var payload = _repository.FindPayload(sessionId, key.CheckpointId);
        if (payload is null)
        {
            // 按 SDK 约定（ICheckpointManager.LookupCheckpointAsync XML doc）：not found 抛 KeyNotFoundException
            throw new KeyNotFoundException(
                $"Checkpoint '{key.CheckpointId}' not found for session '{sessionId}' in WorkflowCheckpoints table.");
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            return new ValueTask<JsonElement>(doc.RootElement.Clone());
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Checkpoint payload 反序列化失败：sessionId={SessionId}, checkpointId={CheckpointId}, bytes={Size}。可能是 SDK 版本升级导致 schema 不兼容。",
                sessionId, key.CheckpointId, payload.Length);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 不支持 <paramref name="withParent"/> 过滤（与 <see cref="FileSystemJsonCheckpointStore"/> 实现一致：
    /// SDK 在该参数为 null 时返回全部，需要过滤时由调用方自行筛）。
    /// </remarks>
    public override ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(string sessionId, CheckpointInfo? withParent = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return new ValueTask<IEnumerable<CheckpointInfo>>(Array.Empty<CheckpointInfo>());
        }

        var rows = _repository.ListByTaskId(sessionId);
        var infos = new List<CheckpointInfo>(rows.Count);
        foreach (var (checkpointId, _) in rows)
        {
            infos.Add(new CheckpointInfo(sessionId, checkpointId));
        }
        return new ValueTask<IEnumerable<CheckpointInfo>>(infos);
    }
}
