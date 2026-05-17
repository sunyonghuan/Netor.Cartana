using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Netor.Cortana.AI.Workflow.DynamicAgents;

/// <summary>
/// P2-2：进程内动态子智能体注册表（任务级生命周期）。
/// </summary>
/// <remarks>
/// 详见 Docs/未来版本策划/聊天式任务发起与动态智能体/01-P2方案设计.md §2-A。
///
/// 设计要点：
/// - 数据结构：<c>ConcurrentDictionary&lt;TaskId, List&lt;DynamicAgentRecord&gt;&gt;</c>。
///   List 内部访问加 lock（<see cref="DynamicAgentRegistry"/> 是 Singleton，多个任务并发安全）。
/// - 任务级隔离：每个 taskId 有独立的 List，互不干扰。
/// - 生命周期：<see cref="WorkflowExecutor.RunWorkflowAsync"/> finally 调用 <see cref="ClearTask"/> 销毁。
///
/// 调用方：
/// - <see cref="CreateSubAgentTool"/>: <see cref="Register"/>, <see cref="GetByName"/>, <see cref="CountByTask"/>
/// - <see cref="DynamicAgentToolsProvider"/>: <see cref="GetByTask"/>（每次 Provider invocation 读最新）
/// - <see cref="WorkflowExecutor.RunWorkflowAsync"/>: <see cref="ClearTask"/>
/// </remarks>
public sealed class DynamicAgentRegistry(ILogger<DynamicAgentRegistry> logger)
{
    /// <summary>
    /// taskId → 该任务下的所有动态子智能体列表。
    /// 内层 List 访问加 lock 防并发问题（List&lt;T&gt; 不是线程安全）。
    /// </summary>
    private readonly ConcurrentDictionary<string, List<DynamicAgentRecord>> _byTask = new();

    /// <summary>
    /// 注册一条动态子智能体记录。
    /// 调用前应先用 <see cref="GetByName"/> 检查 name 唯一性 + <see cref="CountByTask"/> 检查上限。
    /// </summary>
    public void Register(string taskId, DynamicAgentRecord record)
    {
        ArgumentException.ThrowIfNullOrEmpty(taskId);
        ArgumentNullException.ThrowIfNull(record);

        var list = _byTask.GetOrAdd(taskId, _ => []);
        lock (list)
        {
            list.Add(record);
        }

        logger.LogInformation(
            "动态子智能体已注册：name={Name}, taskId={TaskId}, manager={Manager}, tools=[{Tools}]",
            record.Name, taskId, record.CreatedByManagerAgentId, string.Join(",", record.RequiredTools));
    }

    /// <summary>
    /// 获取任务下所有已注册的动态子智能体（快照副本，调用方可安全遍历）。
    /// </summary>
    public IReadOnlyList<DynamicAgentRecord> GetByTask(string taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return [];
        if (!_byTask.TryGetValue(taskId, out var list)) return [];

        // 返回副本避免调用方遍历期间被并发修改
        lock (list)
        {
            return [.. list];
        }
    }

    /// <summary>
    /// 按 name 查找任务下的动态子智能体（同任务内 name 唯一，由 <see cref="CreateSubAgentTool"/> 校验保证）。
    /// </summary>
    public DynamicAgentRecord? GetByName(string taskId, string name)
    {
        if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(name)) return null;
        if (!_byTask.TryGetValue(taskId, out var list)) return null;

        lock (list)
        {
            return list.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// 任务下已注册的动态子智能体数量。<see cref="CreateSubAgentTool"/> 用它做 maxSubAgents 上限校验。
    /// </summary>
    public int CountByTask(string taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return 0;
        if (!_byTask.TryGetValue(taskId, out var list)) return 0;

        lock (list)
        {
            return list.Count;
        }
    }

    /// <summary>
    /// 任务结束时清理 Registry 释放内存。
    /// 由 <see cref="WorkflowExecutor.RunWorkflowAsync"/> finally 块调用。
    /// </summary>
    public void ClearTask(string taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return;

        if (_byTask.TryRemove(taskId, out var list))
        {
            logger.LogInformation(
                "任务 {TaskId} 的动态子智能体 Registry 已清理（释放 {Count} 条记录）",
                taskId, list.Count);
        }
    }
}
