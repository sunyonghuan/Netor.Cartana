using System.Text.Json.Serialization;

namespace Netor.Cortana.Entitys.ModelCapability;

public sealed record ModelCapabilityPrompt
{
    [JsonPropertyName("system")]
    public string? System { get; init; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    [JsonPropertyName("inputFormat")]
    public string? InputFormat { get; init; }

    [JsonPropertyName("input")]
    public string? Input { get; init; }
}

public sealed record ModelCapabilityOutput
{
    [JsonPropertyName("format")]
    public string? Format { get; init; }

    [JsonPropertyName("schemaId")]
    public string? SchemaId { get; init; }
}

public sealed record ModelCapabilityBudget
{
    [JsonPropertyName("maxInputTokens")]
    public int MaxInputTokens { get; init; }

    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; init; }
}

public sealed record ModelCapabilityOptions
{
    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("topP")]
    public double? TopP { get; init; }
}

public sealed record ModelCapabilityRequest
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = string.Empty;

    [JsonPropertyName("pluginId")]
    public string PluginId { get; init; } = string.Empty;

    [JsonPropertyName("purpose")]
    public string? Purpose { get; init; }

    [JsonPropertyName("instruction")]
    public string Instruction { get; init; } = string.Empty;

    [JsonPropertyName("input")]
    public string Input { get; init; } = string.Empty;

    [JsonPropertyName("outputFormat")]
    public string? OutputFormat { get; init; }

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; init; }

    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; init; }
}

public sealed record ModelCapabilityResponse
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "response";

    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; init; } = true;

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}

public sealed record ModelCapabilityError
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "error";

    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = ModelCapabilityProtocol.Protocol;

    [JsonPropertyName("version")]
    public string Version { get; init; } = ModelCapabilityProtocol.Version;

    [JsonPropertyName("op")]
    public string? Op { get; init; }

    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonPropertyName("pluginId")]
    public string? PluginId { get; init; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; init; }

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("retryable")]
    public bool Retryable { get; init; }
}

public sealed record ModelCapabilityConnectedMessage
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "connected";

    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = ModelCapabilityProtocol.Protocol;

    [JsonPropertyName("version")]
    public string Version { get; init; } = ModelCapabilityProtocol.Version;

    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = string.Empty;

    [JsonPropertyName("capabilities")]
    public string Capabilities { get; init; } = "llm.invoke";
}
