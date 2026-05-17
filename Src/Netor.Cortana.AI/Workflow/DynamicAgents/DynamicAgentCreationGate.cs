using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Netor.Cortana.AI.Workflow.DynamicAgents;

/// <summary>
/// P2-4：动态子智能体创建审批闸（Singleton）。
/// </summary>
/// <remarks>
/// 详见 Docs/未来版本策划/聊天式任务发起与动态智能体/03-实施阶段.md §4 + plan §A.2。
///
/// 架构定位：
/// - 与 SDK 的 <c>RequestInfoEvent</c>/<c>StreamingRun.SendResponseAsync</c> 解耦。
///   后者只能在 Magentic plan signoff 路径触发；function-call 内的 <c>CreateSubAgentTool</c> 拿不到这个钩子，
///   因此独立维护一套 Tcs 表 + auto-approve 表。
/// - 与 <see cref="DynamicAgentRegistry"/> 同生命周期：任务结束在
///   <c>WorkflowExecutor.RunWorkflowAsync</c> finally 块调用 <see cref="ClearTask"/>。
///
/// 调用方：
/// - <c>CreateSubAgentTool</c>: <see cref="IsTaskAutoApproved"/> + <see cref="WaitForDecisionAsync"/>
/// - UI（DynamicAgentCreationApprovalVm）: <see cref="ResolveDecision"/> + <see cref="EnableAutoApproveForTask"/>
/// - <c>WorkflowExecutor</c>: <see cref="ClearTask"/>
/// </remarks>
public sealed class DynamicAgentCreationGate(ILogger<DynamicAgentCreationGate> logger)
{
    /// <summary>RequestId → 等待中的 Tcs。一次审批一条记录。</summary>
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DynamicAgentCreationDecision>> _pending = new();

    /// <summary>本任务是否已点过 ApprovedAll（后续 create_subagent 直接放行）。</summary>
    private readonly ConcurrentDictionary<string, byte> _autoApproveTasks = new();

    /// <summary>RequestId → TaskId 反向索引。<see cref="ClearTask"/> 用它批量取消该任务的 pending 等待。</summary>
    private readonly ConcurrentDictionary<string, string> _requestToTask = new();

    /// <summary>
    /// 任务是否已通过 ApprovedAll 进入"本任务全部批准"模式。
    /// 调用方应在发起请求前先检查这个标志，避免无谓的 publish + wait 往返。
    /// </summary>
    public bool IsTaskAutoApproved(string taskId)
        => !string.IsNullOrEmpty(taskId) && _autoApproveTasks.ContainsKey(taskId);

    /// <summary>
    /// 等待用户对某次 <c>create_subagent</c> 请求的决策。
    /// 调用方应在 <c>publisher.Publish(OnDynamicAgentCreationRequested, ...)</c> 之前调用，
    /// 之后再 publish 事件让 UI 看到请求并通过 <see cref="ResolveDecision"/> 解锁。
    /// </summary>
    /// <param name="taskId">所属任务（用于 ClearTask 时批量取消）。</param>
    /// <param name="requestId">本次请求的唯一 ID（由调用方生成，与发布事件中的 RequestId 一致）。</param>
    /// <param name="ct">任务级取消令牌；用户点"停止任务"时通过此令牌解除等待。</param>
    /// <returns>用户决策。</returns>
    /// <exception cref="OperationCanceledException">ct 取消、任务被 ClearTask 终结、或同 RequestId 重复登记。</exception>
    public async Task<DynamicAgentCreationDecision> WaitForDecisionAsync(
        string taskId,
        string requestId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(taskId);
        ArgumentException.ThrowIfNullOrEmpty(requestId);

        var tcs = new TaskCompletionSource<DynamicAgentCreationDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(requestId, tcs))
        {
            throw new InvalidOperationException(
                $"DynamicAgentCreationGate: requestId '{requestId}' 已在等待中（重复登记）。");
        }
        _requestToTask[requestId] = taskId;

        await using var registration = ct.Register(static state =>
        {
            var (gate, rid) = ((DynamicAgentCreationGate Gate, string RequestId))state!;
            if (gate._pending.TryRemove(rid, out var pending))
            {
                gate._requestToTask.TryRemove(rid, out _);
                pending.TrySetCanceled();
            }
        }, (this, requestId));

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
            _requestToTask.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    /// 用户决策回写。返回 true 表示成功解锁，false 表示请求已不存在（超时/取消/重复响应）。
    /// </summary>
    public bool ResolveDecision(string requestId, DynamicAgentCreationDecision decision)
    {
        if (string.IsNullOrEmpty(requestId)) return false;

        if (_pending.TryGetValue(requestId, out var tcs) && tcs.TrySetResult(decision))
        {
            logger.LogInformation(
                "DynamicAgentCreationGate 已解锁请求 {RequestId}：decision={Decision}",
                requestId, decision);
            return true;
        }
        logger.LogDebug(
            "DynamicAgentCreationGate.ResolveDecision 忽略：requestId={RequestId} 不存在或已被解锁",
            requestId);
        return false;
    }

    /// <summary>
    /// 标记本任务后续 <c>create_subagent</c> 调用全部自动批准。
    /// 由 <see cref="ResolveDecision"/> 决策为 <see cref="DynamicAgentCreationDecision.ApprovedAll"/> 时由调用方触发，
    /// 也可由 UI 直接调用以"提前进入静默模式"。
    /// </summary>
    public void EnableAutoApproveForTask(string taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return;
        if (_autoApproveTasks.TryAdd(taskId, 0))
        {
            logger.LogInformation("任务 {TaskId} 已启用动态子智能体创建 auto-approve 模式", taskId);
        }
    }

    /// <summary>
    /// 任务结束清理：取消该任务下所有 pending 等待 + 移除 auto-approve 标志。
    /// 由 <c>WorkflowExecutor.RunWorkflowAsync</c> finally 块调用，与 <c>DynamicAgentRegistry.ClearTask</c> 并列。
    /// </summary>
    public void ClearTask(string taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return;

        var orphanRequestIds = _requestToTask
            .Where(kv => string.Equals(kv.Value, taskId, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .ToArray();

        var cancelled = 0;
        foreach (var rid in orphanRequestIds)
        {
            if (_pending.TryRemove(rid, out var tcs))
            {
                tcs.TrySetCanceled();
                cancelled++;
            }
            _requestToTask.TryRemove(rid, out _);
        }

        var hadAuto = _autoApproveTasks.TryRemove(taskId, out _);

        if (cancelled > 0 || hadAuto)
        {
            logger.LogInformation(
                "任务 {TaskId} 的 DynamicAgentCreationGate 已清理（取消 {Cancelled} 个 pending 等待，autoApprove={HadAuto}）",
                taskId, cancelled, hadAuto);
        }
    }
}

/// <summary>
/// 用户对动态子智能体创建请求的决策。
/// </summary>
public enum DynamicAgentCreationDecision
{
    /// <summary>批准本次创建（下次 create_subagent 仍会再次询问）。</summary>
    Approved,

    /// <summary>批准本次创建，且本任务后续 create_subagent 全部自动批准（auto-approve）。</summary>
    ApprovedAll,

    /// <summary>拒绝本次创建（工具返回失败字符串给 Manager，Manager 自行重新规划）。</summary>
    Rejected,
}
