using System.Text.Json.Serialization;

namespace Cortana.Plugins.ScriptRunner.Protocol;

/// <summary>
/// get_info 响应体（与 NativePluginInfo 字段对齐）。
/// </summary>
internal sealed record PluginInfo
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
    public List<ToolInfo>? Tools { get; init; }
}

internal sealed record ToolInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("parameters")]
    public List<ToolParameter>? Parameters { get; init; }
}

internal sealed record ToolParameter
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "string";

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; init; } = true;
}

/// <summary>
/// init 请求的 Args 反序列化目标（与 NativePluginInitConfig 字段对齐）。
/// </summary>
internal sealed record InitConfig
{
    [JsonPropertyName("dataDirectory")]
    public string DataDirectory { get; init; } = string.Empty;

    [JsonPropertyName("workspaceDirectory")]
    public string WorkspaceDirectory { get; init; } = string.Empty;

    [JsonPropertyName("pluginDirectory")]
    public string PluginDirectory { get; init; } = string.Empty;

    [JsonPropertyName("wsPort")]
    public int WsPort { get; init; }
}
