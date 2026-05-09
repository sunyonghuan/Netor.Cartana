using System.Text;
using System.Text.Json;

using Cortana.Plugins.Memory.Models;
using Cortana.Plugins.Memory.Serialization;
using Cortana.Plugins.Memory.Services;

using Microsoft.Extensions.Logging;

namespace Cortana.Plugins.Memory.Processing;

public sealed class HostModelMemoryAbstractionGenerator(
    HostModelCapabilityClient hostModel,
    FallbackMemoryAbstractionGenerator fallback,
    ILogger<HostModelMemoryAbstractionGenerator> logger) : IMemoryAbstractionGenerator
{
    public MemoryAbstraction GenerateAbstraction(string agentId, string? workspaceId, string topic, IReadOnlyList<MemoryFragment> fragments, string traceId)
    {
        try
        {
            var json = hostModel.InvokeAsync(
                "memory-abstraction",
                "你是 Cortana 记忆引擎的长期记忆抽象器。只输出 JSON。将多个记忆片段合并成一条稳定的长期记忆抽象。输出对象包含 abstractionType、title、statement、summary、keywords、importance、confidence、stabilityScore。",
                BuildInput(topic, fragments),
                "json",
                CancellationToken.None).GetAwaiter().GetResult();

            var abstraction = ParseAbstraction(json, agentId, workspaceId, topic, fragments);
            if (abstraction is not null) return abstraction;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "宿主模型记忆抽象生成失败，使用降级处理。TraceId={TraceId}", traceId);
        }

        return fallback.GenerateAbstraction(agentId, workspaceId, topic, fragments, traceId);
    }

    private static string BuildInput(string topic, IReadOnlyList<MemoryFragment> fragments)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"topic: {topic}");
        foreach (var fragment in fragments.Take(10))
        {
            builder.AppendLine($"- id: {fragment.Id}");
            builder.AppendLine($"  type: {fragment.MemoryType}");
            builder.AppendLine($"  summary: {fragment.Summary}");
            builder.AppendLine($"  detail: {fragment.Detail}");
        }

        return builder.ToString();
    }

    private static MemoryAbstraction? ParseAbstraction(string? json, string agentId, string? workspaceId, string topic, IReadOnlyList<MemoryFragment> fragments)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var normalized = ExtractJsonObject(json);
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        var item = JsonSerializer.Deserialize(normalized, MemoryHostAbstractionJsonContext.Default.HostAbstractionResult);
        if (item is null || string.IsNullOrWhiteSpace(item.Statement)) return null;

        var now = DateTimeOffset.UtcNow.ToString("O");
        var supportIds = fragments.Take(10).Select(static item => item.Id).ToArray();
        return new MemoryAbstraction
        {
            Id = $"abstraction-{Guid.NewGuid().ToString("N")[..24]}",
            AgentId = agentId,
            WorkspaceId = workspaceId,
            AbstractionType = string.IsNullOrWhiteSpace(item.AbstractionType) ? "topic-summary" : item.AbstractionType!,
            Title = string.IsNullOrWhiteSpace(item.Title) ? topic : item.Title!,
            Statement = item.Statement!,
            Summary = string.IsNullOrWhiteSpace(item.Summary) ? item.Statement! : item.Summary!,
            SupportingMemoryIdsJson = JsonSerializer.Serialize(supportIds, MemoryInternalJsonContext.Default.StringArray),
            KeywordsJson = JsonSerializer.Serialize(item.Keywords is { Length: > 0 } ? item.Keywords : [topic], MemoryInternalJsonContext.Default.StringArray),
            Importance = Clamp(item.Importance, 0.1, 1.0, fragments.Average(static fragment => fragment.Importance)),
            Confidence = Clamp(item.Confidence, 0.1, 1.0, fragments.Average(static fragment => fragment.Confidence)),
            StabilityScore = Clamp(item.StabilityScore, 0.1, 1.0, fragments.Average(static fragment => fragment.Confidence)),
            RetentionScore = fragments.Average(static fragment => fragment.RetentionScore),
            DecayRate = fragments.Average(static fragment => fragment.DecayRate),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static string? ExtractJsonObject(string value)
    {
        var start = value.IndexOf('{');
        var end = value.LastIndexOf('}');
        return start >= 0 && end > start ? value[start..(end + 1)] : null;
    }

    private static double Clamp(double value, double min, double max, double fallbackValue)
    {
        if (double.IsNaN(value) || value <= 0) return fallbackValue;
        return Math.Clamp(value, min, max);
    }
}
