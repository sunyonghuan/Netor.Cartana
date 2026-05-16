using Microsoft.Extensions.Logging;

using Netor.Cortana.AI.Providers;
using Netor.Cortana.Entitys.Services;

namespace Netor.Cortana.AI.Workflow.Bridges;

/// <summary>
/// 阶段 5B Phase 3：Workflow→Chat 回灌服务。
///
/// 把已完成的 Workflow 任务的 <see cref="OrchestrationTaskEntity.FinalReport"/> 作为一条
/// assistant 消息追加到指定 Chat 会话，方便用户在原对话上下文中继续提问 / 引用结论。
///
/// 调用链：
/// 1. UI 在 Workflow 任务详情面板点击 [附加到对话] → 弹 ComboBox 选择 session
/// 2. UI 调用本服务的 <see cref="AttachToConversationAsync"/>
/// 3. 本服务定位 task + 拼装回灌正文 + 调 <see cref="ChatHistoryDataProvider.AppendAssistantMessageAsync"/>
/// 4. ChatHistoryDataProvider 写库 + 发 OnConversationTurnCompleted 事件（携带 sourceTaskId 用于 Memory 去重）
///
/// 决策（详见 Phase 3 §4.2）：
/// - <b>不</b>负责创建新 ChatSession：UI 必须传一个已存在的 sessionId，或任务自身有 SourceSessionId
///   （创建新 session 需要 SDK <c>AgentSession</c> 实例，超出本服务职责）
/// - 回灌正文包含来源提示（任务名 + ID + FinalReport），方便用户在 Chat 历史里识别
/// - 仅当 <c>FinalReport</c> 非空时回灌；任务失败 / 取消时拒绝调用
///
/// 详见 docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §5B.3 / Phase 3 实施计划 §4.2。
/// </summary>
public sealed class WorkflowToChatBackflowService(
    WorkflowTaskRepository taskRepo,
    ChatHistoryDataProvider chatHistoryProvider,
    ILogger<WorkflowToChatBackflowService> logger)
{
    /// <summary>
    /// 把指定 Workflow 任务的最终报告作为 assistant 消息追加到 Chat 会话。
    /// </summary>
    /// <param name="taskId">Workflow 任务 ID。</param>
    /// <param name="targetSessionId">
    /// 目标 Chat 会话 ID。若为 null，则回退到 <c>task.SourceSessionId</c>；
    /// 若两者都没有，抛 <see cref="InvalidOperationException"/>（UI 必须先确保有目标 session）。
    /// </param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实际写入的目标 sessionId（用于 UI 跳转到对应会话）。</returns>
    /// <exception cref="InvalidOperationException">
    /// - 任务不存在；
    /// - 任务未完成或 FinalReport 为空；
    /// - 未指定 targetSessionId 且 task.SourceSessionId 也为空。
    /// </exception>
    public async Task<string> AttachToConversationAsync(
        string taskId,
        string? targetSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(taskId);

        var task = taskRepo.GetById(taskId)
            ?? throw new InvalidOperationException($"任务不存在：{taskId}");

        if (string.IsNullOrEmpty(task.FinalReport))
        {
            throw new InvalidOperationException(
                $"任务 {taskId} 未完成或无最终报告，无法回灌");
        }

        var sessionId = !string.IsNullOrEmpty(targetSessionId)
            ? targetSessionId
            : task.SourceSessionId;

        if (string.IsNullOrEmpty(sessionId))
        {
            throw new InvalidOperationException(
                $"任务 {taskId} 无来源会话（SourceSessionId 为空），" +
                "请在 UI 上下文中先选择一个目标 Chat 会话。");
        }

        // 拼装回灌正文：含来源提示 + 任务 ID + FinalReport（Markdown 风格便于 Chat UI 渲染）
        var title = string.IsNullOrEmpty(task.Title) ? "(未命名)" : task.Title;
        var content = $"> 来自 Workflow 任务 **{title}**\n>\n> 任务 ID：`{taskId}`\n\n{task.FinalReport}";

        var messageId = await chatHistoryProvider.AppendAssistantMessageAsync(
            sessionId: sessionId,
            content: content,
            agentId: task.ManagerAgentId,
            agentName: task.ManagerAgentName,
            sourceTaskId: taskId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Workflow→Chat 回灌成功：taskId={TaskId} → sessionId={SessionId}, messageId={MessageId}, reportLength={Length}",
            taskId, sessionId, messageId, task.FinalReport?.Length ?? 0);

        return sessionId;
    }
}
