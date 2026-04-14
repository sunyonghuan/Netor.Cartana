using System.Text.Json.Serialization;

namespace Netor.Cortana.Plugin.Native;
/// 描述原生插件的元数据和工具列表。
/// </summary>
public sealed record NativePluginInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; init; }

    [JsonPropertyName("tools")]
    public List<NativeToolInfo>? Tools { get; init; }
}

/// <summary>
/// 原生插件导出的单个工具描述。
/// </summary>
public sealed record NativeToolInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("parameters")]
    public List<NativeToolParameter>? Parameters { get; init; }
}

/// <summary>
/// 原生工具的参数描述。
/// </summary>
public sealed record NativeToolParameter
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 参数类型：string / number / boolean / integer / array / object。
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "string";

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("required")]
    public bool Required { get; init; }
}
