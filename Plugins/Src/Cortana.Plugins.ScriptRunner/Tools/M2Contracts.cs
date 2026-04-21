using System.Text.Json.Serialization;

namespace Cortana.Plugins.ScriptRunner.Tools;

/// <summary>sys_csx_session_create 响应。</summary>
internal sealed record SessionCreateResult
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;
}

/// <summary>sys_csx_session_exec / sys_csx_session_reset / sys_csx_session_close 共用入参。</summary>
internal sealed record SessionExecArgs
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("timeoutMs")]
    public int? TimeoutMs { get; init; }
}

/// <summary>sys_csx_session_reset / sys_csx_session_close 入参。</summary>
internal sealed record SessionIdArgs
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;
}
