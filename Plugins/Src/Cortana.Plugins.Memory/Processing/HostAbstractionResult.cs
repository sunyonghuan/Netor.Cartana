using System.Text.Json.Serialization;

namespace Cortana.Plugins.Memory.Processing;

internal sealed record HostAbstractionResult
{
    [JsonPropertyName("abstractionType")]
    public string? AbstractionType { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("statement")]
    public string? Statement { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("keywords")]
    public string[]? Keywords { get; init; }

    [JsonPropertyName("importance")]
    public double Importance { get; init; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("stabilityScore")]
    public double StabilityScore { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HostAbstractionResult))]
internal partial class MemoryHostAbstractionJsonContext : JsonSerializerContext;
