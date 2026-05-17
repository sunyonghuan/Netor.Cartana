using System.Reflection;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows.Execution;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Netor.Cortana.AI.Providers;

/// <summary>
/// P2-3：在 Magentic 工作流中给 Manager 注入"如何使用 create_subagent / dynamic_agent_xxx"的教学指令。
/// </summary>
/// <remarks>
/// 详见 Docs/未来版本策划/聊天式任务发起与动态智能体/01-P2方案设计.md §2.3 / 03-实施阶段.md §3。
///
/// 设计要点：
/// - 与 <see cref="OrchestrationInstructionsProvider"/> 同模式：通过 <see cref="AIContext.Instructions"/>
///   把模板文本附加到 Manager 的 system prompt（不修改 <see cref="ChatClientAgentOptions.Instructions"/>，
///   方便保留 AgentEntity.Instructions 原值）。
/// - 模板来自嵌入资源 <c>Netor.Cortana.AI.Resources.Prompts.Magentic.DynamicCreation.md</c>，
///   通过 <see cref="Lazy{T}"/> 进程内缓存只加载一次；缺失资源时降级返回空指令并记录一次警告。
/// - <c>{{MaxSubAgents}}</c> 占位由构造时传入的 <c>maxSubAgents</c> 替换，确保和工具校验一致。
/// </remarks>
internal sealed class MagenticDynamicCreationInstructionsProvider : AIContextProvider
{
    /// <summary>嵌入资源逻辑名（与 csproj 的 LogicalName 一致）。</summary>
    private const string ResourceName = "Netor.Cortana.AI.Resources.Prompts.Magentic.DynamicCreation.md";

    /// <summary>占位符：由 maxSubAgents 替换。</summary>
    private const string MaxSubAgentsPlaceholder = "{{MaxSubAgents}}";

    /// <summary>模板缓存：进程内只读一次。</summary>
    private static readonly Lazy<string?> Template = new(LoadTemplate, isThreadSafe: true);

    private readonly int _maxSubAgents;
    private readonly ILogger _logger;

    public MagenticDynamicCreationInstructionsProvider(int maxSubAgents, ILogger logger)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSubAgents);
        ArgumentNullException.ThrowIfNull(logger);

        _maxSubAgents = maxSubAgents;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var template = Template.Value;
        if (string.IsNullOrEmpty(template))
        {
            return new ValueTask<AIContext>(new AIContext());
        }

        var instructions = template.Replace(
            MaxSubAgentsPlaceholder,
            _maxSubAgents.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

        return new ValueTask<AIContext>(new AIContext { Instructions = instructions });
    }

    private static string? LoadTemplate()
    {
        var assembly = typeof(MagenticDynamicCreationInstructionsProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            // 模板缺失不应阻塞任务执行：降级为空指令（Manager 仍能用工具，只是没有教学）。
            // 真实环境（包含 AOT publish）若发生此分支，说明 EmbeddedResource 配置或 LogicalName 失配。
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
