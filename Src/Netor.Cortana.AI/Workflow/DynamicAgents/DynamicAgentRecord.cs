using Microsoft.Agents.AI;

namespace Netor.Cortana.AI.Workflow.DynamicAgents;

/// <summary>
/// P2-2：Manager 在任务执行中通过 <c>create_subagent</c> 工具创建的临时子智能体记录。
/// 任务级生命周期：由 <see cref="DynamicAgentRegistry"/> 维护，
/// <see cref="WorkflowExecutor.RunWorkflowAsync"/> finally 块调用 <c>ClearTask(taskId)</c> 销毁。
/// </summary>
/// <remarks>
/// 详见 Docs/未来版本策划/聊天式任务发起与动态智能体/01-P2方案设计.md §2-A。
///
/// 设计要点：
/// - <see cref="SubAgent"/> 是已构建好的 <see cref="AIAgent"/> 实例（CreateSubAgentTool 创建时调用 <c>AIAgentFactory.BuildDynamicSubAgent</c>）。
/// - <see cref="RequiredTools"/> 是工具白名单（plugin/MCP toolName），<c>BuildDynamicSubAgent</c> 仅注入匹配项。
/// - <see cref="Name"/> 经过 <c>CreateSubAgentTool</c> 的正则校验（<c>^[a-zA-Z][a-zA-Z0-9_]{5,19}$</c>），同任务内唯一。
/// </remarks>
/// <param name="Name">子智能体名称（6-20 字符字母数字下划线，字母开头）。函数名格式 <c>dynamic_agent_{Name}</c>。</param>
/// <param name="Instructions">系统提示词（告诉子智能体做什么、怎么做）。</param>
/// <param name="Responsibility">职责描述（一句话简介，用于工具描述 + UI 显示）。</param>
/// <param name="RequiredTools">必需的工具列表（plugin/MCP toolName）。空集合表示不需要工具。</param>
/// <param name="CreatedAt">创建时间（UTC）。</param>
/// <param name="CreatedByManagerAgentId">创建该子智能体的 Manager AgentId（用于审计）。</param>
/// <param name="SubAgent">已构建好的 <see cref="AIAgent"/> 实例（瞬态，无 AgentEntity 表）。</param>
public sealed record DynamicAgentRecord(
    string Name,
    string Instructions,
    string Responsibility,
    IReadOnlyList<string> RequiredTools,
    DateTimeOffset CreatedAt,
    string CreatedByManagerAgentId,
    AIAgent SubAgent);
