using Microsoft.Agents.AI.Workflows;

namespace Netor.Cortana.AI.Workflow;

/// <summary>
/// 阶段 5B：追踪一个等待 HITL（人在回路）响应的 Workflow 任务上下文。
/// 由 <c>WorkflowExecutor._pausedTasks</c>（<c>ConcurrentDictionary&lt;string, PendingHitlContext&gt;</c>）持有。
///
/// 生命周期：
/// 1. 事件循环收到 <see cref="RequestInfoEvent"/> → 构造此 context 并插入 _pausedTasks
/// 2. 等待 <see cref="Tcs"/>.Task 解锁（外部通过 <c>ResumeAsync</c> 调用 SetResult）
/// 3. 解锁后用 <see cref="Request"/> 和返回的 <see cref="ExternalResponse"/> 调用 <c>Run.SendResponseAsync</c>
/// 4. 从 _pausedTasks 移除
///
/// 取消语义：
/// - Tcs.SetResult(null) → 表示用户拒绝（rejected），事件循环抛出 OperationCanceledException 走 HandleTaskCancelled
/// - Tcs.TrySetCanceled() → 由外部 ct 触发（例如任务超时、宿主关闭）
///
/// 详见：docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §5B.1。
/// </summary>
internal sealed record PendingHitlContext(
    string TaskId,
    string RequestId,
    ExternalRequest Request,
    StreamingRun Run,
    TaskCompletionSource<ExternalResponse?> Tcs,
    DateTimeOffset PausedAt);
