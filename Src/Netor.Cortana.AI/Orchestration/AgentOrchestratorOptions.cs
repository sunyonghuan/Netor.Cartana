namespace Netor.Cortana.AI.Orchestration;

/// <summary>
/// Chat 模式编排器的阈值参数。阶段 2A 采用常量形式，阶段 3A 起部分迁移到 SystemSettingsService。
/// 详见 docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §2A.5。
/// </summary>
public sealed class AgentOrchestratorOptions
{
    /// <summary>编排最大轮次（保留给未来代码层强制；阶段 2A 实际由模型自行控制）。</summary>
    public int MaxRounds { get; init; } = 5;

    /// <summary>单轮编排最多允许调用的子任务数（阶段 1 已在 instructions 中软约束 MaxSubTasksHint=3）。</summary>
    public int MaxSubTasks { get; init; } = 6;

    /// <summary>单步超时（保留，阶段 2A 不强制）。</summary>
    public TimeSpan PerStepTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>HandoffChat 最大转接链路长度（阶段 3A 起生效）。</summary>
    public int HandoffMaxChainLength { get; init; } = 3;
}
