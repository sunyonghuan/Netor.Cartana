using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

// SDK 的 Workflow 类型名与本项目 namespace `Netor.Cortana.AI.Workflow` 同名，
// 编译器会优先解析为命名空间，因此用类型别名消除歧义。
using SdkWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace Netor.Cortana.AI.Workflow.Builders;

/// <summary>
/// 阶段 4B：把 SDK <see cref="MagenticWorkflowBuilder"/> 的调用细节封装为单一职责工厂。
/// 输入是 Manager + Members 集合，输出是 SDK 原生 <see cref="SdkWorkflow"/>，由 <c>WorkflowExecutor</c>
/// 通过 <see cref="InProcessExecution.RunStreamingAsync"/> 启动并消费 <see cref="WorkflowEvent"/> 流。
///
/// 设计要点（对齐 [04] §4B.2）：
/// - <c>participants[0]</c> 是 Manager（规划者），其余是 Members（执行者）。
/// - <see cref="MagenticWorkflowBuilder.WithMaxRounds"/> 默认覆盖 <see cref="WorkflowExecutorOptions.MaxRounds"/> = 8。
/// - <see cref="MagenticWorkflowBuilder.WithMaxResets"/> = <see cref="WorkflowExecutorOptions.MagenticMaxResets"/>（默认 3）。
/// - <see cref="MagenticWorkflowBuilder.WithMaxStalls"/> = <see cref="WorkflowExecutorOptions.MagenticMaxStalls"/>（默认 3）。
/// - <see cref="MagenticWorkflowBuilder.RequirePlanSignoff"/> = <see cref="WorkflowExecutorOptions.MagenticRequirePlanSignoff"/>
///   阶段 4B 强制锁定为 <c>false</c>（无 HITL UI 时会死锁，[04] §4B.7）；
///   阶段 5B 起 HITL UI 已就绪，<c>WorkflowExecutor.HandleHitlRequestAsync</c> 会处理 RequestInfoEvent，
///   配置可经 SystemSettings 改 <c>true</c>（决策 5B-G：默认仍 false，用户主动开启）。
///
/// SDK 标记 <c>MAAIW001</c> 为 <see cref="System.Diagnostics.CodeAnalysis.ExperimentalAttribute"/>，
/// 整段调用链上下文用 <c>#pragma</c> 抑制以避免 CS9204 警告污染（与 SDK sample 一致做法）。
///
/// 详见：docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §阶段 4B.2 / §4B.7 / §4B.9 / §5B.1。
/// </summary>
internal static class MagenticWorkflowFactory
{
    /// <summary>
    /// 构建 Magentic <see cref="SdkWorkflow"/>。
    /// </summary>
    /// <param name="participants">参与者列表，<c>participants[0]</c> 是 Manager，其余是 Members。至少需 1 个（Manager）。</param>
    /// <param name="taskId">任务 ID，用于设置 workflow 名称便于诊断。</param>
    /// <param name="options">编排器选项，用于读取 Magentic 相关阈值。</param>
    /// <param name="logger">日志输出器。</param>
    /// <returns>SDK 原生 <see cref="SdkWorkflow"/>，由 <c>InProcessExecution.RunStreamingAsync</c> 启动。</returns>
    /// <exception cref="InvalidOperationException">参与者为空。</exception>
    public static SdkWorkflow Build(
        IReadOnlyList<AIAgent> participants,
        string taskId,
        WorkflowExecutorOptions options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentException.ThrowIfNullOrEmpty(taskId);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (participants.Count < 1)
        {
            throw new InvalidOperationException(
                "Magentic 子模式至少需要 1 个 Manager。请到设置中创建至少 1 个智能体。");
        }

        var manager = participants[0];

        // P2-2 修复 2026-05-17：Manager self-talk 模式（用户决策 A）。
        // 工作流模式（Magentic）设计意图：用户只选 Manager，子智能体由 Manager 通过 create_subagent 工具自主创建。
        // 但 SDK MagenticOrchestrator.cs:265 在 NextSpeaker 为空时 fallback 调用 team.First()，team 不能为空。
        // 解决方案：当用户没有显式指定 Members 时，让 Manager 同时充当 team[0]（self-talk），
        // SDK 视角看到 1 个 manager + 1 个 team member（实际是同一个 AIAgent 实例），用户视角无感知。
        // 详见 Docs/未来版本策划/聊天式任务发起与动态智能体/01-P2方案设计.md §2-C "P2-2-Bug 决策 A"。
        var isSelfTalk = participants.Count == 1;
        var team = isSelfTalk
            ? new List<AIAgent> { manager }  // self-talk fallback：Manager 同时充当 team[0]
            : participants.Skip(1).ToList();

#pragma warning disable MAAIW001 // SDK Experimental（Microsoft.Agents.AI.Workflows）
        var workflow = new MagenticWorkflowBuilder(manager)
            .WithName($"Magentic-{taskId}")
            .AddParticipants(team)
            .WithMaxRounds(options.MaxRounds)
            .WithMaxResets(options.MagenticMaxResets)
            .WithMaxStalls(options.MagenticMaxStalls)
            .RequirePlanSignoff(options.MagenticRequirePlanSignoff) // 默认 false；阶段 5B HITL UI 就绪后可经 SystemSettings 改 true
            .Build();
#pragma warning restore MAAIW001

        logger.LogInformation(
            "Magentic workflow 已构建：taskId={TaskId}, manager={Manager}, members={Count}, selfTalk={SelfTalk}, " +
            "maxRounds={Rounds}, maxResets={Resets}, maxStalls={Stalls}, signoff={Signoff}",
            taskId, manager.Name ?? manager.Id, team.Count, isSelfTalk,
            options.MaxRounds, options.MagenticMaxResets, options.MagenticMaxStalls,
            options.MagenticRequirePlanSignoff);

        return workflow;
    }
}
