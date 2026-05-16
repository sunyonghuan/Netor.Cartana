namespace Netor.Cortana.AI.Workflow.Bridges;

/// <summary>
/// 阶段 5B Phase 3：Chat→Workflow 启发式检测器。
///
/// 一期实现：纯关键词启发式（避免引入 LLM 调用成本和延迟）。
/// 当 user input 同时满足以下条件时被判定为"复杂任务"：
/// 1. 长度 ≥ 30 字符（短问题大概率是简单 chat）
/// 2. 包含至少一个复杂任务关键词
///
/// 阶段 6 可升级为 LLM 分类（替换实现，外部接口不变）：
/// - 把 user input 喂给 manager-agent 的 chat client，让它判断 task complexity
/// - 注意成本：会增加每次 chat 的延迟（建议做缓存或离线分类）
///
/// 详见 docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §5B.3 / Phase 3 实施计划 §4.1.1。
/// </summary>
public sealed class WorkflowSuggestionDetector
{
    /// <summary>
    /// 复杂任务关键词列表（中英文混合）。
    /// 命中即视为可能的工作模式候选。
    /// </summary>
    private static readonly string[] ComplexTaskKeywords =
    [
        // 中文：项目级动作
        "做一个", "实现", "完整", "设计一个", "搭建", "调研并", "对比分析",
        "全面", "方案", "整套", "完整版", "从零开始",
        // 中文：研究 / 调查
        "调研", "深度分析", "综合", "全方位",
        // 英文
        "build a", "implement", "design", "comprehensive", "research and",
        "compare and", "analyze and", "from scratch",
    ];

    /// <summary>最小输入长度（字符数）。低于此长度即视为简单 chat。</summary>
    public int MinInputLength { get; init; } = 30;

    /// <summary>
    /// 判断 user input 是否像是一个复杂任务（适合切到 Workflow）。
    /// </summary>
    /// <param name="userInput">用户输入文本。</param>
    /// <returns>true：建议显示 banner 引导切到工作模式；false：保持 Chat 路径。</returns>
    public bool IsLikelyComplexTask(string? userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput) || userInput.Length < MinInputLength)
        {
            return false;
        }

        foreach (var kw in ComplexTaskKeywords)
        {
            if (userInput.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
