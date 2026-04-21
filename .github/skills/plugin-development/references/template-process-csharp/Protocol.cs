using System.Text.Json.Serialization;

namespace MyProcessPlugin;

public sealed record HostRequest
{
    [JsonPropertyName("method")] public string Method { get; init; } = "";
    [JsonPropertyName("toolName")] public string? ToolName { get; init; }
    [JsonPropertyName("args")] public string? Args { get; init; }
}

public sealed record HostResponse
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("data")] public string? Data { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
}

public sealed record PluginInfo
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("version")] public string Version { get; init; } = "1.0.0";
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("instructions")] public string? Instructions { get; init; }
    [JsonPropertyName("tags")] public string[]? Tags { get; init; }
    [JsonPropertyName("tools")] public ToolSpec[]? Tools { get; init; }
}

public sealed record ToolSpec
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("description")] public string Description { get; init; } = "";
    [JsonPropertyName("parameters")] public ParamSpec[]? Parameters { get; init; }
}

public sealed record ParamSpec
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("type")] public string Type { get; init; } = "string";
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("required")] public bool Required { get; init; }
}

public sealed record InitConfig
{
    [JsonPropertyName("dataDirectory")] public string DataDirectory { get; init; } = "";
    [JsonPropertyName("workspaceDirectory")] public string WorkspaceDirectory { get; init; } = "";
    [JsonPropertyName("pluginDirectory")] public string PluginDirectory { get; init; } = "";
    [JsonPropertyName("wsPort")] public int WsPort { get; init; }
}

public sealed record EchoArgs
{
    [JsonPropertyName("text")] public string Text { get; init; } = "";
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(HostRequest))]
[JsonSerializable(typeof(HostResponse))]
[JsonSerializable(typeof(PluginInfo))]
[JsonSerializable(typeof(InitConfig))]
[JsonSerializable(typeof(EchoArgs))]
internal partial class AppJsonContext : JsonSerializerContext;
