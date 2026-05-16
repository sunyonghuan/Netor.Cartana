namespace Netor.Cortana.AI.Workflow;

/// <summary>
/// Workflow 任务列表查询参数。决策 7-A：对齐 Chat 风格的列表查询能力。
/// 详见 docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §2B.2。
/// </summary>
public sealed record WorkflowTaskListQuery
{
    /// <summary>工作区 ID。空字符串表示不过滤。</summary>
    public required string WorkspaceId { get; init; }

    /// <summary>是否包含归档项；默认 false（与 Chat 列表一致）。</summary>
    public bool IncludeArchived { get; init; }

    /// <summary>状态过滤（可空，传 null 表示不过滤）。</summary>
    public IReadOnlyList<string>? Statuses { get; init; }

    /// <summary>每页数量，默认 30。</summary>
    public int Limit { get; init; } = 30;

    /// <summary>分页偏移，默认 0。</summary>
    public int Offset { get; init; }

    /// <summary>
    /// 阶段 6 Phase 3：标题搜索关键词（决策 6-3-A 子串 LIKE 匹配）。
    /// null 或空字符串表示不过滤。
    /// 详见 docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §阶段 6 #3。
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    /// P1 群聊真实化：子模式过滤（收尾决策 DT-9，2026-05-16 落地）。
    /// null 或空集合 = 不过滤；非空 = 仅返回 SubMode 在该列表中的任务。
    /// - 「工作流」tab 传 ["magentic", "parallelanalysis"]
    /// - 「群聊」tab 传 ["groupchat"]
    /// 详见 Docs/未来版本策划/界面重设计/05-阶段总结.md §3.1（DT-9）+ §6.2 中期遗留事项。
    /// </summary>
    public IReadOnlyList<string>? SubModes { get; init; }
}
