using System.Text.Json.Serialization;

namespace Cortana.Plugins.ScriptRunner.Tools;

/// <summary>sys_csx_run_file 参数。</summary>
internal sealed record RunFileArgs
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("timeoutMs")]
    public int? TimeoutMs { get; init; }
}

/// <summary>sys_csx_eval 参数。</summary>
internal sealed record EvalArgs
{
    [JsonPropertyName("expr")]
    public string Expression { get; init; } = string.Empty;

    [JsonPropertyName("timeoutMs")]
    public int? TimeoutMs { get; init; }
}

/// <summary>sys_csx_check / sys_csx_format 参数。</summary>
internal sealed record CodeArgs
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;
}

/// <summary>sys_csx_check 响应。</summary>
internal sealed record CheckResult
{
    [JsonPropertyName("diagnostics")]
    public List<DiagnosticInfo> Diagnostics { get; init; } = [];
}

/// <summary>单条诊断信息。</summary>
internal sealed record DiagnosticInfo
{
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "Info";

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("column")]
    public int Column { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>sys_csx_format 响应。</summary>
internal sealed record FormatResult
{
    [JsonPropertyName("formatted")]
    public string Formatted { get; init; } = string.Empty;
}
