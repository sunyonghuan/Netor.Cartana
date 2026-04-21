using System.Text.Json.Serialization;

namespace Netor.Cortana.Plugin.Process.Protocol;

/// <summary>
/// 插件元数据，<c>get_info</c> 响应中 <c>data</c> 字段的反序列化目标。
/// 由 Generator 从 <see cref="PluginAttribute"/> 和所有 <see cref="ToolAttribute"/> 方法提取。
/// </summary>
public sealed record PluginInfoData
{
    /// <summary>插件唯一标识。</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>插件名称。</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>插件版本。</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>插件描述。</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>AI 系统指令片段（可选）。</summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    /// <summary>分类标签。</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>工具列表。</summary>
    [JsonPropertyName("tools")]
    public IReadOnlyList<ToolInfoData> Tools { get; init; } = [];
}

/// <summary>
/// 单个工具的元数据。
/// </summary>
public sealed record ToolInfoData
{
    /// <summary>工具名（snake_case）。</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>工具描述，告诉 AI 这个工具做什么。</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>参数列表。</summary>
    [JsonPropertyName("parameters")]
    public IReadOnlyList<ParameterInfoData>? Parameters { get; init; }
}

/// <summary>
/// 单个工具参数的元数据。
/// </summary>
public sealed record ParameterInfoData
{
    /// <summary>参数名（snake_case）。</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>参数类型（<c>string</c> / <c>integer</c> / <c>number</c> / <c>boolean</c>）。</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "string";

    /// <summary>参数描述。</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>是否必填。</summary>
    [JsonPropertyName("required")]
    public bool Required { get; init; }
}
