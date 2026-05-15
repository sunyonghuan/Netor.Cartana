using Microsoft.Extensions.AI;

using Netor.Cortana.Entitys;

namespace Netor.Cortana.AI.Workflow;

/// <summary>
/// Workflow 任务执行器接口。承接 Magentic / GroupChat / ParallelAnalysis / HandoffExecution 等
/// 顶层 Workflow 模式（不走 Chat 路径，不走 IAgentOrchestrator）。
/// 阶段 2B 占位实现：仅打通数据库 + 事件路径，不接入真实 SDK Workflow；
/// 阶段 3B 起接入 GroupChat（Discussion），阶段 4B 起接入 Magentic / ParallelAnalysis；
/// 阶段 5B 起接入 HITL（人在回路）暂停 / 恢复。
/// 详见 docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §2B.3 / §5B.1。
/// </summary>
public interface IWorkflowExecutor
{
    // ──── 任务生命周期 ────

    /// <summary>启动新任务，返回 taskId。同步立即写库 + 发送 task.started 事件。</summary>
    Task<string> StartTaskAsync(WorkflowTaskRequest request, CancellationToken cancellationToken);

    /// <summary>取消运行中的任务。返回 true 表示已发取消信号。</summary>
    Task<bool> CancelTaskAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>查询任务当前状态。</summary>
    Task<WorkflowTaskStatus> GetTaskStatusAsync(string taskId, CancellationToken cancellationToken);

    // ──── 任务列表与浏览（决策 7-A：对齐 ChatHistoryPanel） ────

    /// <summary>按工作区列表查询任务。</summary>
    Task<IReadOnlyList<OrchestrationTaskEntity>> ListTasksAsync(
        WorkflowTaskListQuery query,
        CancellationToken cancellationToken);

    /// <summary>查询任务详情，未找到时返回 null。</summary>
    Task<OrchestrationTaskDetail?> GetTaskDetailAsync(string taskId, CancellationToken cancellationToken);

    // ──── 列表元数据操作 ────

    /// <summary>设置置顶状态。</summary>
    Task SetPinnedAsync(string taskId, bool pinned, CancellationToken cancellationToken);

    /// <summary>设置归档状态。</summary>
    Task SetArchivedAsync(string taskId, bool archived, CancellationToken cancellationToken);

    /// <summary>用户手动重命名标题。</summary>
    Task RenameTitleAsync(string taskId, string newTitle, CancellationToken cancellationToken);

    /// <summary>删除任务（CASCADE 级联删除 Participant / Step / Message）。</summary>
    Task DeleteTaskAsync(string taskId, CancellationToken cancellationToken);

    // ──── 复制为新任务（决策 10-A） ────

    /// <summary>
    /// 从源任务构造新任务请求模板。调用方可基于此请求二次编辑后调用 StartTaskAsync。
    /// 新请求继承 Mode/SubMode/MemberAgentIds/Manager/InitialInput/Attachments 等字段，
    /// 但 Title 留空、SourceTaskId 记录原任务 ID。
    /// </summary>
    Task<WorkflowTaskRequest> BuildRequestFromTemplateAsync(string sourceTaskId, CancellationToken cancellationToken);

    // ──── HITL：暂停 / 恢复（阶段 5B 新增） ────

    /// <summary>
    /// 恢复一个 paused 任务（响应 HITL 请求）。仅在任务处于 Status==Paused 状态时有效。
    /// 阶段 5B 引入（决策 5B-A：用 SDK <c>StreamingRun.SendResponseAsync</c>）。
    /// </summary>
    /// <param name="taskId">paused 任务 ID。</param>
    /// <param name="requestId">原 RequestInfoEvent 的 RequestId（用于配对，防止旧响应误生效）。</param>
    /// <param name="action">
    ///   <list type="bullet">
    ///     <item><c>"approved"</c> - 批准，发送空 ChatMessage 列表（Magentic 视为通过）</item>
    ///     <item><c>"revised"</c> - 提供修改建议，<paramref name="revisionMessages"/> 不为空</item>
    ///     <item><c>"rejected"</c> - 拒绝，触发 OperationCanceledException 走 HandleTaskCancelled 路径</item>
    ///   </list>
    /// </param>
    /// <param name="revisionMessages">action="revised" 时的修改建议；其他 action 忽略。</param>
    /// <returns>
    /// true：响应已送达并解锁运行；
    /// false：任务不在 paused 状态 / RequestId 不配对 / 任务不存在。
    /// </returns>
    Task<bool> ResumeAsync(
        string taskId,
        string requestId,
        string action,
        IReadOnlyList<ChatMessage>? revisionMessages,
        CancellationToken cancellationToken);
}
