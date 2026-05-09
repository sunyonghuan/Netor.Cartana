using System.Text.Json.Serialization;

namespace Cortana.Plugins.Memory.Services;

/// <summary>
/// Memory 插件向宿主模型能力控制面发送的大模型调用请求。
/// </summary>
internal sealed record HostModelCapabilityRequest
{
    /// <summary>消息类型。当前固定为 <c>request</c>。</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "request";

    /// <summary>请求唯一标识，用于匹配响应和日志追踪。</summary>
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = string.Empty;

    /// <summary>发起调用的插件标识。</summary>
    [JsonPropertyName("pluginId")]
    public string PluginId { get; init; } = string.Empty;

    /// <summary>调用目的，例如记忆语义提取或抽象生成。</summary>
    [JsonPropertyName("purpose")]
    public string? Purpose { get; init; }

    /// <summary>任务指令，用于约束宿主大模型输出。</summary>
    [JsonPropertyName("instruction")]
    public string Instruction { get; init; } = string.Empty;

    /// <summary>业务输入内容。</summary>
    [JsonPropertyName("input")]
    public string Input { get; init; } = string.Empty;

    /// <summary>期望输出格式，例如 <c>json</c> 或 <c>text</c>。</summary>
    [JsonPropertyName("outputFormat")]
    public string? OutputFormat { get; init; }

    /// <summary>本次调用的超时时间，单位毫秒。</summary>
    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; init; }

    /// <summary>最大输出 token 数。</summary>
    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; init; }
}

/// <summary>
/// 宿主模型能力返回给 Memory 插件的统一响应。
/// </summary>
internal sealed record HostModelCapabilityResponse
{
    /// <summary>消息类型。当前固定为 <c>response</c>。</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>请求唯一标识。</summary>
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = string.Empty;

    /// <summary>本次调用是否成功。</summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>宿主大模型返回的最终文本。</summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>失败时的错误代码。</summary>
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    /// <summary>失败时的错误说明。</summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 插件清单中的最小元数据。
/// </summary>
internal sealed record HostPluginManifestMetadata
{
    /// <summary>插件标识。</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>插件版本。</summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }
}

/// <summary>
/// Memory 插件宿主模型能力协议的 JSON 源生成上下文。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HostModelCapabilityRequest))]
[JsonSerializable(typeof(HostModelCapabilityResponse))]
[JsonSerializable(typeof(HostPluginManifestMetadata))]
internal partial class MemoryHostModelCapabilityJsonContext : JsonSerializerContext;
