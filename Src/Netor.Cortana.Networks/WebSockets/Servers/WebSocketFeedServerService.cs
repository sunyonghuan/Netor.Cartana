using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Netor.Cortana.AI.Providers;
using Netor.Cortana.Entitys;
using Netor.Cortana.Entitys.ModelCapability;
using Netor.Cortana.Entitys.Services;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Netor.Cortana.Networks;

/// <summary>
/// 独立端口的对话事实 Feed 服务。仅承载 conversation-feed（订阅与历史回放）。
/// </summary>
public sealed class WebSocketFeedServerService(
    ILogger<WebSocketFeedServerService> logger,
    SystemSettingsService settingsService,
    CortanaDbContext db,
    IPluginModelCapabilityService modelCapabilityService) : IHostedService, IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _clientLocks = new();
    private readonly ConcurrentDictionary<string, byte> _subscriptions = new();
    private readonly ConcurrentDictionary<string, WebSocket> _modelCapabilityClients = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _modelCapabilityClientLocks = new();

    public int Port { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var configured = settingsService.GetValue<int>("ConversationFeed.Port", 0);
        Port = configured > 0 ? configured : GetRandomPort();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var thread = new Thread(() =>
        {
            _listener = new HttpListener();
            try
            {
                _listener.Prefixes.Add($"http://localhost:{Port}{CortanaWsEndpoints.ConversationFeedPath}");
                _listener.Prefixes.Add($"http://localhost:{Port}{ModelCapabilityProtocol.Path}");
                _listener.Start();
            }
            catch (HttpListenerException ex)
            {
                logger.LogWarning(ex, "Feed 端口 {Port} 绑定失败，回退随机端口", Port);
                _listener.Close();
                _listener = new HttpListener();
                Port = GetRandomPort();
                _listener.Prefixes.Add($"http://localhost:{Port}{CortanaWsEndpoints.ConversationFeedPath}");
                _listener.Prefixes.Add($"http://localhost:{Port}{ModelCapabilityProtocol.Path}");
                _listener.Start();
            }

            logger.LogInformation("Conversation Feed 服务器已启动，端口：{Port}", Port);
            AcceptLoopAsync(_cts.Token).GetAwaiter().GetResult();
        })
        { IsBackground = true, Name = "ConversationFeedServer" };
        thread.Start();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try { _cts?.Cancel(); } catch { }
        foreach (var (id, ws) in _clients) await CloseAsync(id, ws);
        foreach (var (id, ws) in _modelCapabilityClients) await CloseAsync(id, ws);
        _clients.Clear();
        foreach (var l in _clientLocks.Values) l.Dispose();
        _clientLocks.Clear();
        _subscriptions.Clear();
        _modelCapabilityClients.Clear();
        foreach (var l in _modelCapabilityClientLocks.Values) l.Dispose();
        _modelCapabilityClientLocks.Clear();
        _listener?.Close();
        _cts?.Dispose();
        _cts = null;
        logger.LogInformation("Conversation Feed 服务器已停止");
    }

    public async Task BroadcastConversationFeedAsync(string message, CancellationToken cancellationToken = default)
    {
        foreach (var (clientId, _) in _subscriptions)
        {
            await SendAsync(clientId, message, cancellationToken);
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is { IsListening: true })
        {
            try
            {
                var http = await _listener.GetContextAsync().WaitAsync(ct);
                var path = Normalize(http.Request.Url?.AbsolutePath);
                if (path == Normalize(CortanaWsEndpoints.ConversationFeedPath) && http.Request.IsWebSocketRequest)
                {
                    await AcceptClientAsync(http, ct);
                    continue;
                }
                if (path == Normalize(ModelCapabilityProtocol.Path) && http.Request.IsWebSocketRequest)
                {
                    await AcceptModelCapabilityClientAsync(http, ct);
                    continue;
                }
                http.Response.StatusCode = 404; http.Response.Close();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Feed 接入异常"); }
        }
    }

    private async Task AcceptClientAsync(HttpListenerContext http, CancellationToken ct)
    {
        var wsContext = await http.AcceptWebSocketAsync(null);
        var id = Guid.NewGuid().ToString("N");
        var ws = wsContext.WebSocket;
        _clients[id] = ws; _clientLocks[id] = new SemaphoreSlim(1, 1);

        var welcome = JsonSerializer.Serialize(new ConversationFeedControlMessage
        {
            Type = "connected",
            ClientId = id,
            Protocol = CortanaWsEndpoints.ConversationFeedProtocol,
            Version = CortanaWsEndpoints.ConversationFeedVersion,
            Topics = [CortanaWsEndpoints.ConversationTopic]
        }, WebSocketJsonContext.Default.ConversationFeedControlMessage);
        await SendAsync(id, welcome, ct);
        _ = ReceiveLoopAsync(id, ws, ct);
    }

    private async Task AcceptModelCapabilityClientAsync(HttpListenerContext http, CancellationToken ct)
    {
        var wsContext = await http.AcceptWebSocketAsync(null);
        var id = Guid.NewGuid().ToString("N");
        var ws = wsContext.WebSocket;
        _modelCapabilityClients[id] = ws;
        _modelCapabilityClientLocks[id] = new SemaphoreSlim(1, 1);

        var welcome = JsonSerializer.Serialize(new ModelCapabilityConnectedMessage
        {
            ClientId = id
        }, WebSocketJsonContext.Default.ModelCapabilityConnectedMessage);
        await SendModelCapabilityAsync(id, welcome, ct);
        _ = ReceiveModelCapabilityLoopAsync(id, ws, ct);
    }

    private async Task ReceiveLoopAsync(string id, WebSocket ws, CancellationToken ct)
    {
        var buf = new byte[8192];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var res = await ws.ReceiveAsync(buf, ct);
                if (res.MessageType == WebSocketMessageType.Close) break;
                var json = Encoding.UTF8.GetString(buf, 0, res.Count);
                await HandleMessageAsync(id, json, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex) { logger.LogWarning(ex, "Feed 通信异常 {Client}", id); }
        finally { await CloseAsync(id, ws); }
    }

    private async Task ReceiveModelCapabilityLoopAsync(string id, WebSocket ws, CancellationToken ct)
    {
        var buf = new byte[16 * 1024];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var text = await ReadTextMessageAsync(ws, buf, ct);
                if (text is null) break;
                await HandleModelCapabilityMessageAsync(id, text, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex) { logger.LogWarning(ex, "Model capability 通信异常 {Client}", id); }
        finally { await CloseModelCapabilityAsync(id, ws); }
    }

    private async Task HandleModelCapabilityMessageAsync(string id, string json, CancellationToken ct)
    {
        ModelCapabilityRequest? request = null;
        try
        {
            request = JsonSerializer.Deserialize(json, WebSocketJsonContext.Default.ModelCapabilityRequest);
            if (request is null)
            {
                await SendModelCapabilityErrorAsync(id, null, "INVALID_REQUEST", "请求内容为空。", false, ct);
                return;
            }

            var response = await modelCapabilityService.InvokeAsync(request, ct);
            var payload = JsonSerializer.Serialize(response, WebSocketJsonContext.Default.ModelCapabilityResponse);
            await SendModelCapabilityAsync(id, payload, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            await SendModelCapabilityErrorAsync(id, request, "UNAUTHORIZED_CAPABILITY", ex.Message, false, ct);
        }
        catch (TimeoutException ex)
        {
            await SendModelCapabilityErrorAsync(id, request, "TIMEOUT", ex.Message, true, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendModelCapabilityErrorAsync(id, request, "MODEL_NOT_CONFIGURED", ex.Message, false, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "处理 model-capability 请求失败：{Json}", json);
            await SendModelCapabilityErrorAsync(id, request, "INTERNAL_ERROR", ex.Message, true, ct);
        }
    }

    private async Task HandleMessageAsync(string id, string json, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            if (string.Equals(type, "subscribe", StringComparison.Ordinal))
            {
                var protocol = root.TryGetProperty("protocol", out var p) ? p.GetString() ?? string.Empty : string.Empty;
                if (!string.Equals(protocol, CortanaWsEndpoints.ConversationFeedProtocol, StringComparison.Ordinal))
                {
                    await SendErrorAsync(id, "protocol 不匹配", ct); return;
                }
                _subscriptions[id] = 0;
                var ack = JsonSerializer.Serialize(new ConversationFeedControlMessage
                {
                    Type = "subscribed",
                    ClientId = id,
                    Protocol = CortanaWsEndpoints.ConversationFeedProtocol,
                    Version = CortanaWsEndpoints.ConversationFeedVersion,
                    Topics = [CortanaWsEndpoints.ConversationTopic]
                }, WebSocketJsonContext.Default.ConversationFeedControlMessage);
                await SendAsync(id, ack, ct); return;
            }
            if (string.Equals(type, "replay", StringComparison.Ordinal))
            {
                var since = root.TryGetProperty("sinceTimestamp", out var s) ? s.GetInt64() : 0L;
                var batch = root.TryGetProperty("batchSize", out var b) ? Math.Clamp(b.GetInt32(), 100, 2000) : 500;
                await SendReplayBatchesAsync(id, since, batch, ct); return;
            }
            logger.LogWarning("Feed 收到未知消息类型：{Type}", type);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Feed 消息解析失败：{Json}", json); }
    }

    private async Task SendReplayBatchesAsync(string clientId, long sinceTimestamp, int batchSize, CancellationToken ct)
    {
        try
        {
            var total = 0; var last = sinceTimestamp;
            while (true)
            {
                var rows = db.Query(
                    $"""
                    SELECT
                        m.Id,
                        m.SessionId,
                        m.Role,
                        m.Content,
                        m.CreatedTimestamp,
                        m.ModelName,
                        m.AgentId AS MessageAgentId,
                        m.AgentName AS MessageAgentName,
                        s.Categorize AS WorkspaceId,
                        s.AgentId AS SessionAgentId,
                        s.RawDiscription
                    FROM ChatMessages m
                    LEFT JOIN ChatSessions s ON s.Id = m.SessionId
                    WHERE m.CreatedTimestamp >= @Since
                    ORDER BY m.CreatedTimestamp
                    LIMIT {batchSize}
                    """,
                    ConversationExportRecordMapper.Read,
                    cmd => cmd.Parameters.AddWithValue("@Since", last));
                if (rows.Count == 0)
                {
                    var completed = JsonSerializer.Serialize(new ConversationFeedEventMessage
                    {
                        Type = "event",
                        Protocol = CortanaWsEndpoints.ConversationFeedProtocol,
                        Version = CortanaWsEndpoints.ConversationFeedVersion,
                        Topic = CortanaWsEndpoints.ConversationTopic,
                        EventType = "conversation.export.completed",
                        Payload = JsonDocument.Parse($"{{\"total\":{total}}}").RootElement
                    }, WebSocketJsonContext.Default.ConversationFeedEventMessage);
                    await SendAsync(clientId, completed, ct); break;
                }
                total += rows.Count; last = rows[^1].CreatedTimestamp + 1;
                var batch = new ConversationExportBatch { BatchId = Guid.NewGuid().ToString("N"), HasMore = true, Items = rows.ToArray() };
                var payload = JsonSerializer.SerializeToElement(batch, WebSocketJsonContext.Default.ConversationExportBatch);
                var msg = JsonSerializer.Serialize(new ConversationFeedEventMessage
                {
                    Type = "event",
                    Protocol = CortanaWsEndpoints.ConversationFeedProtocol,
                    Version = CortanaWsEndpoints.ConversationFeedVersion,
                    Topic = CortanaWsEndpoints.ConversationTopic,
                    EventType = "conversation.export.batch",
                    Payload = payload
                }, WebSocketJsonContext.Default.ConversationFeedEventMessage);
                await SendAsync(clientId, msg, ct);
            }
            logger.LogInformation("Conversation replay 完成：Since={Since}, Total={Total}", sinceTimestamp, total);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Conversation replay 失败");
            await SendErrorAsync(clientId, $"replay failed: {ex.Message}", ct);
        }
    }

    private async Task SendAsync(string id, string text, CancellationToken ct)
    {
        if (!_clients.TryGetValue(id, out var ws) || ws.State != WebSocketState.Open) return;
        if (!_clientLocks.TryGetValue(id, out var l)) return;
        await l.WaitAsync(ct);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally { l.Release(); }
    }

    private async Task SendModelCapabilityAsync(string id, string text, CancellationToken ct)
    {
        if (!_modelCapabilityClients.TryGetValue(id, out var ws) || ws.State != WebSocketState.Open) return;
        if (!_modelCapabilityClientLocks.TryGetValue(id, out var l)) return;
        await l.WaitAsync(ct);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally { l.Release(); }
    }

    private Task SendErrorAsync(string id, string message, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new ConversationFeedControlMessage
        {
            Type = "error",
            ClientId = id,
            Protocol = CortanaWsEndpoints.ConversationFeedProtocol,
            Version = CortanaWsEndpoints.ConversationFeedVersion,
            Message = message
        }, WebSocketJsonContext.Default.ConversationFeedControlMessage);
        return SendAsync(id, payload, ct);
    }

    private Task SendModelCapabilityErrorAsync(
        string id,
        ModelCapabilityRequest? request,
        string code,
        string message,
        bool retryable,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new ModelCapabilityResponse
        {
            RequestId = request?.RequestId ?? string.Empty,
            Success = false,
            ErrorCode = code,
            ErrorMessage = message
        }, WebSocketJsonContext.Default.ModelCapabilityResponse);

        return SendModelCapabilityAsync(id, payload, ct);
    }

    private static async Task CloseAsync(string id, WebSocket ws)
    {
        if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "服务器关闭", CancellationToken.None); } catch { }
        }
        ws.Dispose();
    }

    private async Task CloseModelCapabilityAsync(string id, WebSocket ws)
    {
        _modelCapabilityClients.TryRemove(id, out _);
        if (_modelCapabilityClientLocks.TryRemove(id, out var sendLock)) sendLock.Dispose();

        if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "服务器关闭", CancellationToken.None); } catch { }
        }
        ws.Dispose();
    }

    private static async Task<string?> ReadTextMessageAsync(WebSocket ws, byte[] buffer, CancellationToken ct)
    {
        using var message = new MemoryStream();
        while (true)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.Count > 0) message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }

        return Encoding.UTF8.GetString(message.ToArray());
    }

    private static string Normalize(string? path) => string.IsNullOrWhiteSpace(path) ? "/" : (path.EndsWith("/") ? path : path + "/");
    private static int GetRandomPort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p;
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        _cts?.Dispose();
        _cts = null;
        foreach (var c in _clients.Values) c.Dispose();
        foreach (var l in _clientLocks.Values) l.Dispose();
        foreach (var c in _modelCapabilityClients.Values) c.Dispose();
        foreach (var l in _modelCapabilityClientLocks.Values) l.Dispose();
        _clients.Clear(); _clientLocks.Clear(); _subscriptions.Clear();
        _modelCapabilityClients.Clear(); _modelCapabilityClientLocks.Clear();
        _listener?.Close();
    }
}
