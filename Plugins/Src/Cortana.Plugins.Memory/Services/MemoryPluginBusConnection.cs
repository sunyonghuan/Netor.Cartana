using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Cortana.Plugins.Memory.Serialization;

using Microsoft.Extensions.Logging;

namespace Cortana.Plugins.Memory.Services;

/// <summary>
/// 维护 Memory 插件到宿主 PluginBus 的单条长连接，提供串行发送、请求等待和 ping/pong 心跳处理能力。
/// </summary>
public sealed class MemoryPluginBusConnection(ILogger<MemoryPluginBusConnection> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<HostModelCapabilityResponse?>> _modelPendingRequests = new(StringComparer.Ordinal);
    private ClientWebSocket? _socket;

    /// <summary>
    /// 指示当前 WebSocket 是否处于打开状态。
    /// </summary>
    public bool IsOpen => _socket?.State == WebSocketState.Open;

    /// <summary>
    /// 连接指定 PluginBus 端点。
    /// </summary>
    /// <param name="endpoint">PluginBus WebSocket 端点。</param>
    /// <param name="cancellationToken">取消连接的令牌。</param>
    public async Task ConnectAsync(string endpoint, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        await CloseAsync().ConfigureAwait(false);
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(new Uri(endpoint), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 读取一条完整的文本消息。
    /// </summary>
    /// <param name="cancellationToken">取消读取的令牌。</param>
    /// <returns>文本消息；连接关闭时返回 <see langword="null"/>。</returns>
    public async Task<string?> ReadTextMessageAsync(CancellationToken cancellationToken)
    {
        var socket = _socket ?? throw new InvalidOperationException("PluginBus 尚未连接。");
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.Count > 0) message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }

        return Encoding.UTF8.GetString(message.ToArray());
    }

    /// <summary>
    /// 串行发送文本消息，避免同一 WebSocket 并发发送。
    /// </summary>
    /// <param name="json">要发送的 JSON 文本。</param>
    /// <param name="cancellationToken">取消发送的令牌。</param>
    public async Task SendTextAsync(string json, CancellationToken cancellationToken)
    {
        var socket = _socket ?? throw new InvalidOperationException("PluginBus 尚未连接。");
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// 回复宿主心跳 ping。
    /// </summary>
    /// <param name="cancellationToken">取消发送的令牌。</param>
    public Task SendPongAsync(CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new PluginBusPongFrame(), MemoryPluginBusJsonContext.Default.PluginBusPongFrame);
        return SendTextAsync(payload, cancellationToken);
    }

    /// <summary>
    /// 通过共享长连接发送模型能力请求，并等待匹配的响应。
    /// </summary>
    /// <param name="request">模型能力请求。</param>
    /// <param name="cancellationToken">取消等待的令牌。</param>
    /// <returns>模型能力响应；超时或断开时返回 <see langword="null"/>。</returns>
    internal async Task<HostModelCapabilityResponse?> InvokeModelAsync(HostModelCapabilityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsOpen) return null;

        var pending = new TaskCompletionSource<HostModelCapabilityResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_modelPendingRequests.TryAdd(request.RequestId, pending)) return null;

        try
        {
            var json = JsonSerializer.Serialize(request, MemoryHostModelCapabilityJsonContext.Default.HostModelCapabilityRequest);
            await SendTextAsync(json, cancellationToken).ConfigureAwait(false);

            var timeoutMs = request.TimeoutMs > 0 ? request.TimeoutMs : 120_000;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
            await using var registration = timeoutCts.Token.Register(static state =>
            {
                var source = (TaskCompletionSource<HostModelCapabilityResponse?>)state!;
                source.TrySetResult(null);
            }, pending);

            return await pending.Task.ConfigureAwait(false);
        }
        finally
        {
            _modelPendingRequests.TryRemove(request.RequestId, out _);
        }
    }

    /// <summary>
    /// 尝试完成模型能力响应。
    /// </summary>
    /// <param name="json">宿主返回的响应 JSON。</param>
    /// <returns>如果响应匹配到等待中的请求，则为 <see langword="true"/>。</returns>
    public bool TryCompleteModelResponse(string json)
    {
        try
        {
            var response = JsonSerializer.Deserialize(json, MemoryHostModelCapabilityJsonContext.Default.HostModelCapabilityResponse);
            if (response is null || string.IsNullOrWhiteSpace(response.RequestId)) return false;
            if (_modelPendingRequests.TryRemove(response.RequestId, out var pending))
            {
                pending.TrySetResult(response);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "解析模型能力响应失败。");
        }

        return false;
    }

    /// <summary>
    /// 关闭 PluginBus 连接。
    /// </summary>
    public async Task CloseAsync()
    {
        foreach (var pending in _modelPendingRequests.Values)
        {
            pending.TrySetResult(null);
        }
        _modelPendingRequests.Clear();

        var socket = _socket;
        if (socket is null) return;

        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch
        {
        }
        finally
        {
            socket.Dispose();
            _socket = null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        _sendLock.Dispose();
    }
}

/// <summary>
/// PluginBus pong 心跳帧。
/// </summary>
internal sealed record PluginBusPongFrame
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "pong";
}

/// <summary>
/// PluginBus 基础心跳帧 JSON 上下文。
/// </summary>
[JsonSerializable(typeof(PluginBusPongFrame))]
internal partial class MemoryPluginBusJsonContext : JsonSerializerContext;
