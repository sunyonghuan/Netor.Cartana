namespace Cortana.Plugins.ScriptRunner.Tools;

using System.Text.Json.Serialization;

/// <summary>sys_csx_run_str 的参数结构。</summary>
internal sealed record RunStrArgs
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("timeoutMs")]
    public int? TimeoutMs { get; init; }
}

/// <summary>执行类 Tool 的统一返回结构。</summary>
internal sealed record RunResult
{
    [JsonPropertyName("returnValue")]
    public string? ReturnValue { get; init; }

    [JsonPropertyName("returnType")]
    public string? ReturnType { get; init; }

    [JsonPropertyName("stdout")]
    public string? Stdout { get; init; }

    [JsonPropertyName("stderr")]
    public string? Stderr { get; init; }

    [JsonPropertyName("elapsedMs")]
    public long ElapsedMs { get; init; }
}
