using System.Text.Json.Serialization;

namespace Cortana.Plugins.Memory.Models;

public sealed record MemoryContextSupplyRequest
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "request";

    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = MemoryContextSupplyProtocol.Protocol;

    [JsonPropertyName("version")]
    public string Version { get; init; } = MemoryContextSupplyProtocol.Version;

    [JsonPropertyName("op")]
    public string Op { get; init; } = MemoryContextSupplyProtocol.SupplyRequestOperation;

    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("agentId")]
    public string? AgentId { get; init; }

    [JsonPropertyName("agentName")]
    public string? AgentName { get; init; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; init; }

    [JsonPropertyName("workspaceDirectory")]
    public string? WorkspaceDirectory { get; init; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    [JsonPropertyName("sessionTitle")]
    public string? SessionTitle { get; init; }

    [JsonPropertyName("turnId")]
    public string? TurnId { get; init; }

    [JsonPropertyName("messageId")]
    public string? MessageId { get; init; }

    [JsonPropertyName("scenario")]
    public string? Scenario { get; init; }

    [JsonPropertyName("currentTask")]
    public string? CurrentTask { get; init; }

    [JsonPropertyName("recentMessages")]
    public IReadOnlyList<MemoryContextMessage> RecentMessages { get; init; } = [];

    [JsonPropertyName("triggerSource")]
    public string? TriggerSource { get; init; }

    [JsonPropertyName("maxMemoryCount")]
    public int MaxMemoryCount { get; init; } = 8;

    [JsonPropertyName("maxTokenBudget")]
    public int MaxTokenBudget { get; init; } = 1200;

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; init; } = 250;

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }
}

public sealed record MemoryContextMessage
{
    [JsonPropertyName("messageId")]
    public string? MessageId { get; init; }

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; init; }
}

public sealed record MemoryContextSupplyPackage
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "response";

    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = MemoryContextSupplyProtocol.Protocol;

    [JsonPropertyName("version")]
    public string Version { get; init; } = MemoryContextSupplyProtocol.Version;

    [JsonPropertyName("op")]
    public string Op { get; init; } = MemoryContextSupplyProtocol.SupplyPackageOperation;

    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("groups")]
    public IReadOnlyList<MemoryContextSupplyGroup> Groups { get; init; } = [];

    [JsonPropertyName("items")]
    public IReadOnlyList<MemoryContextSupplyItem> Items { get; init; } = [];

    [JsonPropertyName("budget")]
    public MemoryContextSupplyBudget Budget { get; init; } = new();

    [JsonPropertyName("appliedPolicy")]
    public MemoryContextSupplyPolicy AppliedPolicy { get; init; } = new();

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonPropertyName("producerVersion")]
    public string ProducerVersion { get; init; } = string.Empty;
}

public sealed record MemoryContextSupplyGroup
{
    [JsonPropertyName("groupKey")]
    public string GroupKey { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("items")]
    public IReadOnlyList<MemoryContextSupplyItem> Items { get; init; } = [];

    [JsonPropertyName("priority")]
    public int Priority { get; init; }
}

public sealed record MemoryContextSupplyItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("topic")]
    public string Topic { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("score")]
    public double Score { get; init; }

    [JsonPropertyName("sourceRecallScore")]
    public double SourceRecallScore { get; init; }

    [JsonPropertyName("lifecycleState")]
    public string LifecycleState { get; init; } = string.Empty;

    [JsonPropertyName("confirmationState")]
    public string ConfirmationState { get; init; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; init; }
}

public sealed record MemoryContextSupplyBudget
{
    [JsonPropertyName("maxMemoryCount")]
    public int MaxMemoryCount { get; init; }

    [JsonPropertyName("usedMemoryCount")]
    public int UsedMemoryCount { get; init; }

    [JsonPropertyName("maxTokenBudget")]
    public int MaxTokenBudget { get; init; }

    [JsonPropertyName("estimatedTokens")]
    public int EstimatedTokens { get; init; }
}

public sealed record MemoryContextSupplyPolicy
{
    [JsonPropertyName("supplyEnabled")]
    public bool SupplyEnabled { get; init; }

    [JsonPropertyName("maxMemoryCount")]
    public int MaxMemoryCount { get; init; }

    [JsonPropertyName("recallMinimumConfidence")]
    public double RecallMinimumConfidence { get; init; }

    [JsonPropertyName("ranking")]
    public string Ranking { get; init; } = string.Empty;

    [JsonPropertyName("grouping")]
    public string Grouping { get; init; } = string.Empty;
}

public sealed record MemoryContextSupplyError
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "error";

    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = MemoryContextSupplyProtocol.Protocol;

    [JsonPropertyName("version")]
    public string Version { get; init; } = MemoryContextSupplyProtocol.Version;

    [JsonPropertyName("op")]
    public string Op { get; init; } = MemoryContextSupplyProtocol.SupplyErrorOperation;

    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("retryable")]
    public bool Retryable { get; init; }
}
