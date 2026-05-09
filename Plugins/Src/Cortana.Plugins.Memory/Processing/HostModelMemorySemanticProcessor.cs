using System.Text;
using System.Text.Json;

using Cortana.Plugins.Memory.Models;
using Cortana.Plugins.Memory.Serialization;
using Cortana.Plugins.Memory.Services;

using Microsoft.Extensions.Logging;

namespace Cortana.Plugins.Memory.Processing;

public sealed class HostModelMemorySemanticProcessor(
    HostModelCapabilityClient hostModel,
    FallbackMemorySemanticProcessor fallback,
    ILogger<HostModelMemorySemanticProcessor> logger) : IMemorySemanticProcessor
{
    public IReadOnlyList<MemorySemanticCandidate> ExtractCandidates(ObservationRecord observation, string traceId)
    {
        ArgumentNullException.ThrowIfNull(observation);

        try
        {
            var json = hostModel.InvokeAsync(
                "memory-processing",
                "你是 Cortana 记忆引擎的长期记忆提取器。只输出 JSON。从对话观察中提取稳定、有长期价值的用户画像、用户习惯、偏好、约束、事实或待办。不要记录一次性寒暄、无意义片段和敏感隐私。输出 JSON 数组，每项包含 memoryType、topic、title、summary、detail、keywords、importance、confidence、novelty。",
                BuildInput(observation),
                "json",
                CancellationToken.None).GetAwaiter().GetResult();

            var candidates = ParseCandidates(json, observation);
            if (candidates.Count > 0) return candidates;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "宿主模型记忆语义提取失败，使用降级处理。TraceId={TraceId}", traceId);
        }

        return fallback.ExtractCandidates(observation, traceId);
    }

    private static string BuildInput(ObservationRecord observation)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"role: {observation.Role}");
        builder.AppendLine($"agentId: {observation.AgentId}");
        builder.AppendLine($"workspaceId: {observation.WorkspaceId}");
        builder.AppendLine($"sessionId: {observation.SessionId}");
        builder.AppendLine("content:");
        builder.AppendLine(observation.Content ?? string.Empty);
        return builder.ToString();
    }

    private static IReadOnlyList<MemorySemanticCandidate> ParseCandidates(string? json, ObservationRecord observation)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        var normalized = ExtractJsonArray(json);
        if (string.IsNullOrWhiteSpace(normalized)) return [];

        var items = JsonSerializer.Deserialize(normalized, MemoryHostSemanticJsonContext.Default.HostSemanticCandidateArray) ?? [];
        return items
            .Where(static item => !string.IsNullOrWhiteSpace(item.Summary) || !string.IsNullOrWhiteSpace(item.Detail))
            .Take(5)
            .Select(item => new MemorySemanticCandidate
            {
                MemoryType = NormalizeType(item.MemoryType),
                Topic = string.IsNullOrWhiteSpace(item.Topic) ? NormalizeType(item.MemoryType) : item.Topic!,
                Title = string.IsNullOrWhiteSpace(item.Title) ? item.Topic ?? NormalizeType(item.MemoryType) : item.Title!,
                Summary = item.Summary ?? item.Detail ?? string.Empty,
                Detail = item.Detail ?? item.Summary ?? string.Empty,
                Keywords = item.Keywords ?? [],
                Importance = Clamp(item.Importance, 0.1, 1.0, 0.65),
                Confidence = Clamp(item.Confidence, 0.1, 1.0, 0.6),
                Novelty = Clamp(item.Novelty, 0.1, 1.0, 0.6),
                SourceObservation = observation
            })
            .ToArray();
    }

    private static string? ExtractJsonArray(string value)
    {
        var start = value.IndexOf('[');
        var end = value.LastIndexOf(']');
        return start >= 0 && end > start ? value[start..(end + 1)] : null;
    }

    private static string NormalizeType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "fact";
        return value.Trim().ToLowerInvariant() switch
        {
            "profile" or "user_profile" or "用户画像" => "profile",
            "habit" or "用户习惯" => "habit",
            "preference" or "偏好" => "preference",
            "constraint" or "约束" => "constraint",
            "task" or "todo" or "待办" => "task",
            _ => "fact"
        };
    }

    private static double Clamp(double value, double min, double max, double fallbackValue)
    {
        if (double.IsNaN(value) || value <= 0) return fallbackValue;
        return Math.Clamp(value, min, max);
    }
}
