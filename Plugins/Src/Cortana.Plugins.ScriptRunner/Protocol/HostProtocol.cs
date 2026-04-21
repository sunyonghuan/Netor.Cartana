using System.Text.Json.Serialization;

namespace Cortana.Plugins.ScriptRunner.Protocol;

/// <summary>
/// 宿主 → 插件 stdio 请求。
/// 与 Netor.Cortana.Plugin.Native.NativeHostRequest 字段对齐。
/// </summary>
internal sealed record HostRequest
{
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("toolName")]
    public string? ToolName { get; init; }

    [JsonPropertyName("args")]
    public string? Args { get; init; }
}

/// <summary>
/// 插件 → 宿主 stdio 响应。
/// </summary>
internal sealed record HostResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    public static HostResponse Ok(string? data = null) => new() { Success = true, Data = data };
    public static HostResponse Fail(string error) => new() { Success = false, Error = error };
}
