using System.Text.Json.Serialization;

namespace Netor.Cortana.Networks;

/// <summary>
/// WebSocket 消息 JSON 源生成器上下文（AOT 兼容）。
/// </summary>
[JsonSerializable(typeof(WsMessage))]
internal partial class WebSocketJsonContext : JsonSerializerContext;

/// <summary>
/// WebSocket JSON 消息（替代匿名类型，AOT 兼容）。
/// </summary>
internal sealed record WsMessage
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public string? Data { get; init; }

    [JsonPropertyName("clientId")]
    public string? ClientId { get; init; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }
}
