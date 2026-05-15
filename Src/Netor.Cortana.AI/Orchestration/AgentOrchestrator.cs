using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

using Netor.Cortana.Entitys;
using Netor.Cortana.Entitys.Services;

namespace Netor.Cortana.AI.Orchestration;

/// <summary>
/// 默认 <see cref="IAgentOrchestrator"/> 实现。
/// 阶段 2A 仅做"根据 Mode 委托给 AIAgentFactory"的薄封装，把 Chat 行为从 AiChatHostedService 抽离出来。
/// 阶段 3A 起会在此接入 HandoffChat 分支（AgentWorkflowBuilder.CreateHandoffBuilderWith）。
/// </summary>
public sealed class AgentOrchestrator(
    AIAgentFactory factory,
    AiProviderService providerService,
    AiModelService modelService,
    AgentOrchestratorOptions options,
    ILogger<AgentOrchestrator> logger) : IAgentOrchestrator
{
    private AgentOrchestrationResult? _lastResult;

    /// <summary>
    /// 根据 mentions 数量解析编排模式。阶段 2A 默认规则：
    /// 0 mentions → None（普通对话）；
    /// 1 mention → None（直接对话该 Agent）；
    /// 2+ mentions → ToolDelegation（Coordinator 模式，阶段 1 已实现）。
    /// 阶段 3A 起根据 triage 智能体或显式指令扩展到 HandoffChat。
    /// </summary>
    public static AgentOrchestrationMode ResolveMode(IReadOnlyList<AgentMention> mentions)
        => mentions.Count switch
        {
            0 => AgentOrchestrationMode.None,
            1 => AgentOrchestrationMode.None,
            _ => AgentOrchestrationMode.ToolDelegation,
        };

    public Task<AIAgent> BuildAgentAsync(
        AgentOrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 决策 2A.7：UsedAgentIds 由宿主真实记录提供，不再依赖模型自述。
        // 阶段 2A 时 UsedAgentIds 与 mentions 完全一致；阶段 3A+ 起补充 Handoff 切换记录。
        _lastResult = new AgentOrchestrationResult
        {
            UsedAgentIds = [.. request.Mentions.Select(m => m.Agent.Id)],
            Warnings = [],
            Failures = [],
        };

        AIAgent agent = request.Mode switch
        {
            AgentOrchestrationMode.None
                => factory.Build(request.MainAgent, request.MainProvider, request.MainModel),

            AgentOrchestrationMode.ToolDelegation
                => factory.BuildWithSubAgents(
                    request.MainAgent,
                    request.MainProvider,
                    request.MainModel,
                    [.. request.Mentions],
                    providerService,
                    modelService),

            AgentOrchestrationMode.HandoffChat
                => throw new NotImplementedException(
                    "HandoffChat 模式将在阶段 3A 接入 AgentWorkflowBuilder.CreateHandoffBuilderWith"),

            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Mode,
                "Unknown AgentOrchestrationMode"),
        };

        logger.LogDebug(
            "AgentOrchestrator built agent: mode={Mode}, mainAgent={MainAgent}, mentions={MentionCount}, maxSubTasks={MaxSubTasks}",
            request.Mode, request.MainAgent.Name, request.Mentions.Count, options.MaxSubTasks);

        return Task.FromResult(agent);
    }

    public AgentOrchestrationResult? GetLastResult() => _lastResult;
}
