using System.ComponentModel;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows.Execution;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using Netor.Cortana.AI.Workflow.DynamicAgents;

namespace Netor.Cortana.AI.Providers;

/// <summary>
/// P2-2：把 <see cref="DynamicAgentRegistry"/> 中已注册的动态子智能体作为 <see cref="AIFunction"/> 工具注入 Manager 上下文。
/// </summary>
/// <remarks>
/// 详见 Docs/未来版本策划/聊天式任务发起与动态智能体/01-P2方案设计.md §2-B。
///
/// 设计要点：
/// - 与 <see cref="SubAgentContextProvider"/> 不同的是，本 Provider 每次 <see cref="ProvideAIContextAsync"/> 都
///   从 <see cref="DynamicAgentRegistry"/> 读最新列表 —— 这是支持"运行时动态创建"的关键。
/// - function name 格式：<c>dynamic_agent_{Name}</c>（与 <c>BuildWithSubAgents</c> 中的 <c>agent_{safeIdPart}</c> 区分）。
/// - 包装函数签名简化为单参数 <c>query</c>，不携带附件（动态子智能体由 Manager 在任务执行中临时创建，
///   附件已通过主输入到达 Manager；Manager 在 query 中传递必要信息）。
/// </remarks>
internal sealed class DynamicAgentToolsProvider(
    DynamicAgentRegistry registry,
    string taskId,
    ILogger logger) : AIContextProvider
{
    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        // 关键：每次 invocation 都从 Registry 读最新列表（支持运行时增加）
        var records = registry.GetByTask(taskId);

        if (records.Count == 0)
        {
            return new ValueTask<AIContext>(new AIContext());
        }

        var functions = new List<AIFunction>(records.Count);
        foreach (var record in records)
        {
            functions.Add(WrapDynamicAgentAsFunction(record));
        }

        logger.LogDebug("DynamicAgentToolsProvider 暴露 {Count} 个动态子智能体工具（taskId={TaskId}）",
            functions.Count, taskId);

        return new ValueTask<AIContext>(new AIContext { Tools = functions });
    }

    /// <summary>
    /// 把 <see cref="DynamicAgentRecord"/> 包装为 <see cref="AIFunction"/>。
    /// function name = <c>dynamic_agent_{record.Name}</c>。
    /// </summary>
    private static AIFunction WrapDynamicAgentAsFunction(DynamicAgentRecord record)
    {
        [Description("调用动态子智能体处理查询")]
        async Task<string> InvokeDynamicAgentAsync(
            [Description("传递给子智能体的查询或任务描述")] string query,
            CancellationToken ct)
        {
            var msg = new ChatMessage(ChatRole.User, query);
            var response = await record.SubAgent.RunAsync([msg], cancellationToken: ct).ConfigureAwait(false);
            return response.Text ?? string.Empty;
        }

        return AIFunctionFactory.Create(
            InvokeDynamicAgentAsync,
            new AIFunctionFactoryOptions
            {
                Name = $"dynamic_agent_{record.Name}",
                Description = $"[动态子智能体 {record.Name}] {record.Responsibility}"
            });
    }
}
