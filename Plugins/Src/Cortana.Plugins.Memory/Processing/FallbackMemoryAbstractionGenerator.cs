using System.Text.Json;
using Cortana.Plugins.Memory.Models;
using Cortana.Plugins.Memory.Serialization;

namespace Cortana.Plugins.Memory.Processing;

/// <summary>
/// MCP 独立运行模式下的保守抽象生成器。
/// 在宿主模式下不再作为降级路径使用——抽象层模型失败时直接跳过。
/// </summary>
public sealed class FallbackMemoryAbstractionGenerator : IMemoryAbstractionGenerator
{
    public Task<MemoryAbstraction?> GenerateAbstractionAsync(string agentId, string? workspaceId, string topic, IReadOnlyList<MemoryFragment> fragments, string traceId, CancellationToken cancellationToken = default)
    {
        if (fragments == null || fragments.Count == 0) return Task.FromResult<MemoryAbstraction?>(null);

        var top = fragments.Take(6).ToList();
        var supportingIds = top.Select(f => f.Id).ToArray();
        var supportingSummaries = top
            .Select(static f => f.Summary?.Trim())
            .Where(static summary => !string.IsNullOrWhiteSpace(summary))
            .Select(static summary => summary!)
            .Distinct()
            .ToList();

        var statement = MergeSummariesToStatement(supportingSummaries);
        if (string.IsNullOrWhiteSpace(statement)) return Task.FromResult<MemoryAbstraction?>(null);

        var summary = statement.Length <= 200 ? statement : statement[..200];

        var now = DateTimeOffset.UtcNow.ToString("O");
        var abstraction = new MemoryAbstraction
        {
            Id = $"abstraction-{Guid.NewGuid().ToString("N")[..24]}",
            AgentId = agentId,
            WorkspaceId = workspaceId,
            AbstractionType = "topic-summary",
            Title = topic,
            Statement = statement,
            Summary = summary,
            SupportingMemoryIdsJson = JsonSerializer.Serialize(supportingIds, MemoryInternalJsonContext.Chinese.StringArray),
            KeywordsJson = JsonSerializer.Serialize(new[] { topic }, MemoryInternalJsonContext.Chinese.StringArray),
            Importance = top.Average(f => f.Importance),
            Confidence = Math.Min(1.0, top.Average(f => f.Confidence) + 0.05),
            StabilityScore = top.Average(f => f.Confidence),
            RetentionScore = top.Average(f => f.RetentionScore),
            DecayRate = top.Average(f => f.DecayRate),
            CreatedAt = now,
            UpdatedAt = now
        };

        return Task.FromResult<MemoryAbstraction?>(abstraction);
    }

    private static string MergeSummariesToStatement(IReadOnlyList<string> summaries)
    {
        var pieces = new List<string>();
        foreach (var s in summaries)
        {
            var parts = s.Split(['.', '。', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length == 0) continue;
                if (!pieces.Any(x => x.Equals(t, StringComparison.OrdinalIgnoreCase))) pieces.Add(t);
            }
        }

        return string.Join("; ", pieces);
    }
}
