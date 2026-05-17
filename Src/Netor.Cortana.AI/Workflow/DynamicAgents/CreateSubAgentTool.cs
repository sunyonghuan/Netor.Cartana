using System.ComponentModel;
using System.Text.RegularExpressions;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using Netor.Cortana.Entitys;
using Netor.EventHub;

namespace Netor.Cortana.AI.Workflow.DynamicAgents;

/// <summary>
/// P2-2：<c>create_subagent</c> 工具构造器。
/// 把"创建动态子智能体"作为 <see cref="AIFunction"/> 注入 Manager 工具集，让 Manager 在任务执行中自主创建。
/// </summary>
/// <remarks>
/// 详见 Docs/未来版本策划/聊天式任务发起与动态智能体/01-P2方案设计.md §2-B。
///
/// 使用方式：
/// <code>
/// var tool = CreateSubAgentTool.Create(registry, factory, provider, model,
///     taskId, managerAgentId, maxSubAgents, logger);
/// providers.Add(new SubAgentContextProvider([tool]));
/// </code>
///
/// 校验规则：
/// 1. <c>name</c> 合法：字母开头 + 字母数字下划线 + 6-20 字符（正则 <c>^[a-zA-Z][a-zA-Z0-9_]{5,19}$</c>）
/// 2. <c>name</c> 唯一：同任务内不重复
/// 3. 数量上限：<c>CountByTask &lt; maxSubAgents</c>
/// 4. <c>requiredTools</c> 真实存在（在 PluginLoader 已注册的工具表里）
/// </remarks>
public static class CreateSubAgentTool
{
    /// <summary>name 校验正则：字母开头 + 5-19 个后续字符（共 6-20 字符）。</summary>
    private static readonly Regex NamePattern = new(
        "^[a-zA-Z][a-zA-Z0-9_]{5,19}$",
        RegexOptions.Compiled);

    /// <summary>
    /// 构造 <c>create_subagent</c> AIFunction。
    /// 由 <see cref="AIAgentFactory.BuildSubAgent"/> 在 isManager=true 时调用并注入 Manager 工具集。
    /// </summary>
    /// <param name="registry">动态子智能体注册表。</param>
    /// <param name="factory">智能体工厂（用于 BuildDynamicSubAgent + 工具白名单校验）。</param>
    /// <param name="mainProvider">Manager 当前使用的 Provider，子智能体复用。</param>
    /// <param name="mainModel">Manager 当前使用的 Model，子智能体复用。</param>
    /// <param name="taskId">所属工作流任务 ID。</param>
    /// <param name="managerAgentId">发起请求的 Manager AgentId（用于审计）。</param>
    /// <param name="maxSubAgents">本任务允许创建的子智能体上限。</param>
    /// <param name="gate">P2-4：审批闸（用于 await 用户决策）。</param>
    /// <param name="publisher">P2-4：事件发布器（用于发布 OnDynamicAgentCreationRequested/Resolved）。</param>
    /// <param name="requireApproval">P2-4：是否启用审批（来自 SystemSettings <c>workflow.dynamicAgent.requireApproval</c>，默认 true）。</param>
    /// <param name="logger">日志输出器。</param>
    public static AIFunction Create(
        DynamicAgentRegistry registry,
        AIAgentFactory factory,
        AiProviderEntity mainProvider,
        AiModelEntity mainModel,
        string taskId,
        string managerAgentId,
        int maxSubAgents,
        DynamicAgentCreationGate gate,
        IPublisher publisher,
        bool requireApproval,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(mainProvider);
        ArgumentNullException.ThrowIfNull(mainModel);
        ArgumentException.ThrowIfNullOrEmpty(taskId);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(publisher);

        [Description("创建一个临时子智能体协助任务。子智能体仅在本任务生命周期内有效，任务结束后销毁。")]
        async Task<string> CreateSubAgentAsync(
            [Description("子智能体名称。仅字母数字下划线，6-20 字符，必须字母开头。例：'doc_analyst' / 'code_reviewer_1'")] string name,
            [Description("子智能体的系统提示词。告诉它做什么、怎么做、输出格式等。")] string instructions,
            [Description("职责描述（一句话简介，用于 UI 显示和工具描述）。例：'分析合同条款并提取风险点'")] string responsibility,
            [Description("必需的工具列表（plugin/MCP 工具名）。可空。例：['read_file', 'web_search']")] string[]? requiredTools,
            CancellationToken ct)
        {
            // 1. 名称合法性校验
            if (string.IsNullOrWhiteSpace(name) || !NamePattern.IsMatch(name))
            {
                var msg = $"创建失败：名称不合法（要求字母开头 + 字母数字下划线 + 6-20 字符）。当前：'{name}'";
                logger.LogWarning("[create_subagent] {Reason} (taskId={TaskId})", msg, taskId);
                return msg;
            }

            // 2. 名称唯一性校验
            if (registry.GetByName(taskId, name) is not null)
            {
                var msg = $"创建失败：名称 '{name}' 已存在（同一任务内不可重复）。请选择其他名称。";
                logger.LogWarning("[create_subagent] {Reason} (taskId={TaskId})", msg, taskId);
                return msg;
            }

            // 3. 数量上限校验
            var current = registry.CountByTask(taskId);
            if (current >= maxSubAgents)
            {
                var msg = $"创建失败：已达 maxSubAgents 上限 ({maxSubAgents})。请重新规划任务结构，或在新任务中调整上限。";
                logger.LogWarning("[create_subagent] {Reason} (taskId={TaskId}, current={Current})",
                    msg, taskId, current);
                return msg;
            }

            // 4. requiredTools 真实存在性校验
            if (requiredTools is { Length: > 0 })
            {
                var available = factory.GetAvailableToolNames();
                var unknown = requiredTools
                    .Where(t => !available.Contains(t, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (unknown.Count > 0)
                {
                    var msg = $"创建失败：未知工具 [{string.Join(", ", unknown)}]。请检查工具拼写或先启用对应插件/MCP 服务器。";
                    logger.LogWarning("[create_subagent] {Reason} (taskId={TaskId})", msg, taskId);
                    return msg;
                }
            }

            // 4.5 P2-4：HITL 审批闸
            // 详见 Docs/未来版本策划/聊天式任务发起与动态智能体/03-实施阶段.md §4 plan §A.3。
            // 仅当 requireApproval=true 且本任务未启用 auto-approve 时阻塞等待用户决策；
            // 否则继续走原 5+6+7 步骤直接创建。
            if (requireApproval && !gate.IsTaskAutoApproved(taskId))
            {
                var requestId = Guid.NewGuid().ToString("N");
                publisher.Publish(Events.OnDynamicAgentCreationRequested,
                    new DynamicAgentCreationRequestedArgs(
                        TaskId: taskId,
                        RequestId: requestId,
                        ManagerAgentId: managerAgentId ?? string.Empty,
                        ProposedName: name,
                        ProposedResponsibility: responsibility ?? string.Empty,
                        ProposedInstructions: instructions,
                        ProposedRequiredTools: requiredTools ?? [],
                        CurrentCount: current,
                        MaxSubAgents: maxSubAgents));

                DynamicAgentCreationDecision decision;
                try
                {
                    decision = await gate.WaitForDecisionAsync(taskId, requestId, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // 用户停止任务 / Gate.ClearTask 触发：返回友好字符串而非抛异常，避免污染 Manager 上下文
                    publisher.Publish(Events.OnDynamicAgentCreationResolved,
                        new DynamicAgentCreationResolvedArgs(taskId, requestId, "cancelled"));
                    return "创建失败：审批等待被取消（用户停止了任务）。";
                }

                if (decision == DynamicAgentCreationDecision.Rejected)
                {
                    publisher.Publish(Events.OnDynamicAgentCreationResolved,
                        new DynamicAgentCreationResolvedArgs(taskId, requestId, "rejected"));
                    logger.LogInformation(
                        "[create_subagent] 用户拒绝创建子智能体 '{Name}' (taskId={TaskId})", name, taskId);
                    return $"创建失败：用户拒绝创建子智能体 '{name}'。请重新规划子智能体结构或选择更合适的名称/职责后再试。";
                }

                if (decision == DynamicAgentCreationDecision.ApprovedAll)
                {
                    gate.EnableAutoApproveForTask(taskId);
                }

                publisher.Publish(Events.OnDynamicAgentCreationResolved,
                    new DynamicAgentCreationResolvedArgs(
                        taskId,
                        requestId,
                        decision == DynamicAgentCreationDecision.ApprovedAll ? "approved_all" : "approved"));
            }

            // 5. 用 AIAgentFactory.BuildDynamicSubAgent 构建瞬态 AIAgent
            AIAgent subAgent;
            try
            {
                subAgent = factory.BuildDynamicSubAgent(
                    name, instructions, mainProvider, mainModel, requiredTools);
            }
            catch (Exception ex)
            {
                var msg = $"创建失败：构建子智能体时异常 - {ex.Message}";
                logger.LogError(ex, "[create_subagent] BuildDynamicSubAgent 抛出异常 (taskId={TaskId}, name={Name})",
                    taskId, name);
                return msg;
            }

            // 6. 写 Registry
            var record = new DynamicAgentRecord(
                Name: name,
                Instructions: instructions,
                Responsibility: responsibility ?? string.Empty,
                RequiredTools: requiredTools ?? [],
                CreatedAt: DateTimeOffset.UtcNow,
                CreatedByManagerAgentId: managerAgentId ?? string.Empty,
                SubAgent: subAgent);

            registry.Register(taskId, record);

            // 7. 返回 Manager 友好的成功信息
            var toolsHint = (requiredTools is { Length: > 0 })
                ? $"，可使用工具：{string.Join(", ", requiredTools)}"
                : string.Empty;
            return $"已创建子智能体 '{name}'（{responsibility}{toolsHint}）。" +
                   $"现在可以通过 dynamic_agent_{name}(query) 调用它处理任务。";
        }

        return AIFunctionFactory.Create(
            CreateSubAgentAsync,
            new AIFunctionFactoryOptions
            {
                Name = "create_subagent",
                Description = "创建一个临时子智能体协助任务。返回创建结果文本。" +
                              "子智能体仅在本任务生命周期内有效，任务结束后自动销毁。" +
                              "创建成功后，可通过 dynamic_agent_{name}(query) 调用它。"
            });
    }
}
