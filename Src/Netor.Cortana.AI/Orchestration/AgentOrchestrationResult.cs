namespace Netor.Cortana.AI.Orchestration;

/// <summary>
/// Chat 模式编排结果。由 <see cref="IAgentOrchestrator"/> 在本轮对话过程中收集。
/// 详见 docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §2A.7（"参与智能体"来源切换）。
/// </summary>
public sealed record AgentOrchestrationResult
{
    /// <summary>本轮最终汇总文本。Chat 模式下通常由主 Agent 模型生成，本字段保留以便扩展。</summary>
    public string? FinalText { get; init; }

    /// <summary>本轮实际参与的子智能体 Id 列表（含主 Agent 时由调用方决定是否包含）。</summary>
    public IReadOnlyList<string> UsedAgentIds { get; init; } = [];

    /// <summary>编排过程中收集到的警告信息（如附件丢失、子 Agent 不支持工具等）。</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>编排过程中收集到的失败信息（子 Agent 调用异常、超时等）。</summary>
    public IReadOnlyList<OrchestrationFailure> Failures { get; init; } = [];

    /// <summary>本轮 Token 使用量统计（如可获取）。阶段 2A 可空。</summary>
    public OrchestrationTokenUsage? TokenUsage { get; init; }
}

/// <summary>单条子任务失败信息。</summary>
/// <param name="AgentId">失败的子智能体 Id。</param>
/// <param name="Reason">失败原因（异常消息或超时描述）。</param>
public sealed record OrchestrationFailure(string AgentId, string Reason);

/// <summary>编排过程的 Token 使用量。</summary>
/// <param name="InputTokens">输入 Token 总数。</param>
/// <param name="OutputTokens">输出 Token 总数。</param>
public sealed record OrchestrationTokenUsage(long InputTokens, long OutputTokens);
