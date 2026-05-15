using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

// SDK 的 Workflow 类型名与本项目 namespace `Netor.Cortana.AI.Workflow` 同名，
// 编译器会优先解析为命名空间，因此用类型别名消除歧义。
using SdkWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace Netor.Cortana.AI.Workflow.Builders;

/// <summary>
/// 阶段 4B：把 SDK <see cref="AgentWorkflowBuilder.BuildConcurrent(string, IEnumerable{AIAgent}, System.Func{IList{List{ChatMessage}}, List{ChatMessage}}?)"/>
/// 的调用细节封装为单一职责工厂。
/// 输入是参与者集合（任意顺序），输出是 SDK 原生 <see cref="SdkWorkflow"/>，由 <c>WorkflowExecutor</c> 通过
/// <see cref="InProcessExecution.RunStreamingAsync"/> 启动并消费 <see cref="WorkflowEvent"/> 流。
///
/// 设计要点（对齐 [04] §4B.3）：
/// - 不区分 Manager / Member：所有参与者并发执行同一输入。
/// - 自定义 aggregator 把每个 Agent 的最后一条 Assistant 消息合并为单条 Markdown 文本，方便
///   <c>WorkflowExecutor.ExtractFinalReportFromOutput</c> 走 <c>List&lt;ChatMessage&gt;</c> 分支取出。
/// - aggregator 内只做字符串拼接，无 IO、无异常路径。
///
/// 详见：docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §阶段 4B.3。
/// </summary>
internal static class ParallelAnalysisWorkflowFactory
{
    /// <summary>
    /// 构建 ParallelAnalysis <see cref="SdkWorkflow"/>。
    /// </summary>
    /// <param name="participants">并行参与者集合，至少 1 个。</param>
    /// <param name="taskId">任务 ID，用于设置 workflow 名称便于诊断。</param>
    /// <param name="logger">日志输出器。</param>
    /// <returns>SDK 原生 <see cref="SdkWorkflow"/>，由 <c>InProcessExecution.RunStreamingAsync</c> 启动。</returns>
    /// <exception cref="InvalidOperationException">参与者为空。</exception>
    public static SdkWorkflow Build(
        IReadOnlyList<AIAgent> participants,
        string taskId,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentException.ThrowIfNullOrEmpty(taskId);
        ArgumentNullException.ThrowIfNull(logger);

        if (participants.Count == 0)
        {
            throw new InvalidOperationException(
                "ParallelAnalysis 子模式至少需要 1 个 Agent，请检查 ManagerAgentId / MemberAgentIds 配置。");
        }

        var workflow = AgentWorkflowBuilder.BuildConcurrent(
            workflowName: $"ParallelAnalysis-{taskId}",
            agents: participants,
            aggregator: AggregateAgentResponses);

        logger.LogInformation(
            "ParallelAnalysis workflow 已构建：taskId={TaskId}, agents={Count}",
            taskId, participants.Count);

        return workflow;
    }

    /// <summary>
    /// 合并多个 Agent 的回复为单条 Markdown 格式的 Assistant 消息。
    /// 每段格式：
    /// <code>
    /// --- {AuthorName} ---
    /// {Text}
    /// </code>
    /// 段与段之间用空行分隔。所有 Agent 都未产出回复时返回 fallback 文本。
    /// </summary>
    private static List<ChatMessage> AggregateAgentResponses(IList<List<ChatMessage>> agentResults)
    {
        var sections = new List<string>();

        foreach (var messages in agentResults)
        {
            if (messages.Count == 0) continue;
            var last = messages.LastOrDefault(m => m.Role == ChatRole.Assistant)
                       ?? messages[^1];
            var author = string.IsNullOrWhiteSpace(last.AuthorName) ? "Agent" : last.AuthorName!;
            var text = last.Text ?? string.Empty;
            sections.Add($"--- {author} ---\n{text}");
        }

        var combined = sections.Count == 0
            ? "[ParallelAnalysis 各参与者均未产出回复]"
            : string.Join("\n\n", sections);

        return [new ChatMessage(ChatRole.Assistant, combined)];
    }
}
