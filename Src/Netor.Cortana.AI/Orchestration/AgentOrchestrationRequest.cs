using Netor.Cortana.Entitys;

namespace Netor.Cortana.AI.Orchestration;

/// <summary>
/// Chat 模式编排请求。封装"用户这一轮对话要调谁、怎么调"的信息。
/// 详见 docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §2A.2 / §2A.3。
/// </summary>
public sealed record AgentOrchestrationRequest
{
    /// <summary>主智能体（由用户当前会话选中的智能体）。</summary>
    public required AgentEntity MainAgent { get; init; }

    /// <summary>主智能体所属厂商。</summary>
    public required AiProviderEntity MainProvider { get; init; }

    /// <summary>主智能体使用的模型。</summary>
    public required AiModelEntity MainModel { get; init; }

    /// <summary>用户在当前轮对话中 @ 提及的子智能体列表。可空集合表示无 mention。</summary>
    public IReadOnlyList<AgentMention> Mentions { get; init; } = [];

    /// <summary>本轮编排模式（由 <see cref="AgentOrchestrator.ResolveMode"/> 解析）。</summary>
    public AgentOrchestrationMode Mode { get; init; } = AgentOrchestrationMode.None;

    /// <summary>子任务执行策略，阶段 2A 默认 Sequential。</summary>
    public AgentExecutionStrategy Strategy { get; init; } = AgentExecutionStrategy.Sequential;

    /// <summary>会话 ID（用于 HandoffChat 模式生成 workflow id 等场景；阶段 2A 可空）。</summary>
    public string? SessionId { get; init; }
}
