using System.Text;
using System.Text.Json;

using Cortana.Plugins.Memory.Models;
using Cortana.Plugins.Memory.Serialization;
using Cortana.Plugins.Memory.Services;

using Microsoft.Extensions.Logging;

namespace Cortana.Plugins.Memory.Processing;

/// <summary>
/// 使用宿主大模型能力提取长期记忆候选，模型不可用时降级到本地规则处理器。
/// 所有模型调用通过信号量串行执行，避免并发导致模型端报错。
/// </summary>
public sealed class HostModelMemorySemanticProcessor(
    HostModelCapabilityClient hostModel,
    FallbackMemorySemanticProcessor fallback,
    ILogger<HostModelMemorySemanticProcessor> logger) : IMemorySemanticProcessor
{
    private static readonly SemaphoreSlim _modelLock = new(1, 1);

    public async Task<IReadOnlyList<MemorySemanticCandidate>> ExtractCandidatesAsync(ObservationRecord observation, string traceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);

        await _modelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = await hostModel.InvokeAsync(
                "memory-processing",
                BuildSystemPrompt(),
                BuildInput(observation),
                "json",
                cancellationToken).ConfigureAwait(false);

            var candidates = ParseCandidates(json, observation);
            if (candidates.Count > 0) return candidates;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "宿主模型记忆语义提取失败，使用降级处理。TraceId={TraceId}", traceId);
        }
        finally
        {
            _modelLock.Release();
        }

        // 第二层允许降级到规则处理器（规则处理器质量可接受）
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

    private static string BuildSystemPrompt() => """
你是 Cortana 记忆引擎的长期记忆提取器。你的任务是从一条对话消息中提取**有长期价值**的记忆候选。

## 提取标准（严格遵守）

只提取满足以下条件的信息：
- 用户的个人偏好、习惯、工作方式（如"喜欢简洁代码"、"习惯用 C#"）
- 用户的身份、角色、技能背景（如"是 .NET 开发者"、"做量化交易"）
- 明确的项目约束或规则（如"不用 EF Core"、"必须支持 AOT"）
- 有价值的事实（如"项目用 SQLite 存储"、"工作目录是 E:\xxx"）
- 用户明确交代的待办或长期任务

## 必须跳过（输出空数组 []）

- AI 助手的问候、确认、客套话（"好的"、"没问题"、"我来帮你"）
- AI 助手的中间推理过程、工具调用描述
- 一次性的操作指令（"帮我打开文件"、"运行这个命令"）
- 纯技术输出（代码片段、错误日志、命令输出）
- 内容过短（少于 10 个字）或无实质信息的消息
- 重复已知信息（如果 summary 和之前的记忆高度相似，不要重复提取）

## 输出格式

输出 JSON 数组，每项包含：
- memoryType: "fact" | "preference" | "constraint" | "task" | "note"
- topic: 主题分类词（2-4字，如"编程语言"、"项目架构"、"工作习惯"）
- title: 简短标题（10字以内）
- summary: 一句话摘要（20-50字，必须是陈述句，主语是"用户"）
- detail: 补充细节（可为空）
- keywords: 关键词数组（3-5个）
- importance: 重要性 0-1（偏好/约束 ≥ 0.7，普通事实 0.4-0.6）
- confidence: 置信度 0-1（用户明确说的 ≥ 0.8，推断的 0.4-0.6）
- novelty: 新颖度 0-1（首次出现的信息 ≥ 0.7）

如果消息中没有任何值得记住的长期信息，直接输出空数组：[]
宁可漏提也不要提取垃圾。质量优先于数量。
""";

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
