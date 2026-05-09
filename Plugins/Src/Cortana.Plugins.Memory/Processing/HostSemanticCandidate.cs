using System.Text.Json.Serialization;

namespace Cortana.Plugins.Memory.Processing;

internal sealed record HostSemanticCandidate
{
    [JsonPropertyName("memoryType")]
    public string? MemoryType { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("keywords")]
    public string[]? Keywords { get; init; }

    [JsonPropertyName("importance")]
    public double Importance { get; init; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("novelty")]
    public double Novelty { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HostSemanticCandidate[]))]
internal partial class MemoryHostSemanticJsonContext : JsonSerializerContext;
