---
name: websocket-integration
description: '通过 WebSocket 连接 Cortana AI 对话服务。编写 C# 客户端代码，实现发送消息、接收流式回复、附件传输、系统事件监听。触发关键词：WebSocket、WS 连接、远程对话、流式回复、AI 接入、实时通信。'
user-invocable: true
---

# WebSocket Integration

通过 WebSocket 协议连接 Cortana，实现 AI 对话交互。

## Assets

- scripts/new-websocket-client.ps1
- resources/ws-message-samples.json
- resources/client-checklist.md
- resources/csharp-client-template.md

## 协议概要

| 项目 | 值 |
|------|-----|
| 地址 | `ws://<host>:<port>/ws/` |
| 默认端口 | `52841`（可在系统设置中修改） |
| 编码 | UTF-8 JSON 文本帧 |
| 传输 | 标准 WebSocket（RFC 6455） |

连接成功后服务端推送 `connected` 消息，含 `clientId`。

## 消息类型

### 客户端 → 服务端

| type | data | 说明 |
|------|------|------|
| `send` | 用户消息文本 | 发送对话消息，可附带 `attachments` |
| `stop` | — | 中止当前 AI 回复 |

### 服务端 → 客户端

| type | 字段 | 说明 |
|------|------|------|
| `connected` | `clientId` | 连接成功 |
| `token` | `data` | AI 流式回复文本片段 |
| `done` | `sessionId` | 本轮回复完成 |
| `error` | `data` | 错误信息；`"cancelled"` 表示被中止 |
| `stt_partial` | `data` | 语音识别中间结果 |
| `stt_final` | `data` | 语音识别最终结果 |
| `stt_stopped` | — | 语音识别停止 |
| `tts_started` | — | TTS 开始 |
| `tts_subtitle` | `data` | TTS 字幕文本 |
| `tts_completed` | — | TTS 完成 |
| `chat_completed` | — | 整个对话流程结束（含 TTS） |
| `wakeword_detected` | — | 检测到唤醒词 |

### 附件格式

```json
{
  "type": "send",
  "data": "描述一下这张图",
  "attachments": [
    { "path": "C:\\Photos\\img.jpg", "name": "img.jpg", "type": "image/jpeg" }
  ]
}
```

`path` 必须是 Cortana 进程所在机器的本地路径。

## C# 客户端实现

所有 JSON 序列化必须使用 Source Generator（AOT 要求）。

### 消息类型定义

```csharp
using System.Text.Json.Serialization;

public sealed record WsClientMessage
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("data")] public string? Data { get; init; }
    [JsonPropertyName("attachments")] public List<WsAttachment>? Attachments { get; init; }
}

public sealed record WsServerMessage
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("data")] public string? Data { get; init; }
    [JsonPropertyName("clientId")] public string? ClientId { get; init; }
    [JsonPropertyName("sessionId")] public string? SessionId { get; init; }
}

public sealed record WsAttachment
{
    [JsonPropertyName("path")] public string Path { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
}
```

### JSON Source Generator

```csharp
[JsonSerializable(typeof(WsClientMessage))]
[JsonSerializable(typeof(WsServerMessage))]
[JsonSerializable(typeof(WsAttachment))]
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class CortanaJsonContext : JsonSerializerContext;
```

### 客户端实现

```csharp
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

public sealed class CortanaWsClient : IAsyncDisposable
{
    private readonly ClientWebSocket _ws = new();
    private readonly Uri _uri;
    private string? _clientId;

    public CortanaWsClient(string host = "localhost", int port = 52841)
    {
        _uri = new Uri($"ws://{host}:{port}/ws/");
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _ws.ConnectAsync(_uri, ct);
        var msg = await ReceiveMessageAsync(ct);
        if (msg?.Type == "connected")
            _clientId = msg.ClientId;
    }

    public async IAsyncEnumerable<string> SendAndStreamAsync(
        string text,
        List<WsAttachment>? attachments = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new WsClientMessage
        {
            Type = "send",
            Data = text,
            Attachments = attachments
        };

        var json = JsonSerializer.Serialize(request, CortanaJsonContext.Default.WsClientMessage);
        await _ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);

        while (!ct.IsCancellationRequested)
        {
            var msg = await ReceiveMessageAsync(ct);
            if (msg is null) yield break;

            switch (msg.Type)
            {
                case "token":
                    if (msg.Data is not null) yield return msg.Data;
                    break;
                case "done":
                    yield break;
                case "error":
                    throw new InvalidOperationException(msg.Data ?? "未知错误");
            }
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        var request = new WsClientMessage { Type = "stop" };
        var json = JsonSerializer.Serialize(request, CortanaJsonContext.Default.WsClientMessage);
        await _ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
    }

    private async Task<WsServerMessage?> ReceiveMessageAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return JsonSerializer.Deserialize(ms.ToArray(), CortanaJsonContext.Default.WsServerMessage);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ws.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        _ws.Dispose();
    }
}
```

### 使用示例

```csharp
await using var client = new CortanaWsClient("localhost", 52841);
await client.ConnectAsync();

await foreach (var token in client.SendAndStreamAsync("帮我写一首七言绝句"))
{
    Console.Write(token);
}

// 带附件
var attachments = new List<WsAttachment>
{
    new() { Path = @"C:\Photos\cat.jpg", Name = "cat.jpg", Type = "image/jpeg" }
};
await foreach (var token in client.SendAndStreamAsync("描述这张图片", attachments))
{
    Console.Write(token);
}

// 中止
await client.StopAsync();
```

## Scaffold Script

```powershell
.\skills\websocket-integration\scripts\new-websocket-client.ps1 -OutputDir Samples\WsClient
```

更完整的多文件 C# 模板见 resources/csharp-client-template.md，可直接按文件拆分到项目中。

## csproj 参考（AOT 发布）

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
  </PropertyGroup>
</Project>
```

## AOT 陷阱

| 陷阱 | 修复 |
|------|------|
| 匿名类型序列化 | 改为强类型 record + `JsonSerializerContext` |
| 遗漏 `[JsonSerializable]` | 在 Context 上添加注册 |
| `JsonSerializer.Serialize<T>(obj)` | 改用 `Serialize(obj, Context.Default.T)` |
| `dynamic` / `ExpandoObject` | 改为强类型 |
| 泛型集合未注册 | 注册 `[JsonSerializable(typeof(List<T>))]` |

## 行为约束

- WS 客户端发起的 `send`，AI 回复只发给该客户端；UI/语音发起的对话广播所有 WS 客户端
- 所有客户端共享同一 AI 对话历史
- 同一时刻只处理一个 AI 请求，新请求需等待或 `stop` 当前请求
- 客户端必须忽略未识别的 `type`（向前兼容）
- `path` 必须是 Cortana 进程本地路径
- 无应用层心跳，依赖 WebSocket 协议层 Ping/Pong

## 交互时序

```
客户端                                 Cortana
  │── ws://host:52841/ws/ ──────────▶│  握手
  │◀── connected {clientId} ─────────│
  │── send {data, attachments?} ────▶│  发送
  │◀── token {data} ─────────────────│  ┐
  │◀── token {data} ─────────────────│  │ 流式回复
  │◀── token {data} ─────────────────│  ┘
  │◀── done {sessionId} ────────────│  完成
  │◀── tts_started ─────────────────│  ┐
  │◀── tts_subtitle {data} ────────│  │ TTS
  │◀── tts_completed ───────────────│  ┘
  │◀── chat_completed ──────────────│  流程结束
  │── stop ─────────────────────────▶│  中止
  │◀── error "cancelled" ───────────│
```
