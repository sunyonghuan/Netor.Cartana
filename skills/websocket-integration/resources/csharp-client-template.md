# C# Client Template

## 文件结构

```text
Samples/WsClient/
  CortanaWsClientSample.csproj
  Program.cs
  CortanaWsClient.cs
  WsContracts.cs
  WsJsonContext.cs
```

## CortanaWsClientSample.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <PublishAot>true</PublishAot>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
  </PropertyGroup>
</Project>
```

## Program.cs

```csharp
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
  eventArgs.Cancel = true;
  cts.Cancel();
};

await using var client = new CortanaWsClient("localhost", 52841);
await client.ConnectAsync(cts.Token);

await foreach (var token in client.SendAndStreamAsync("帮我概括今天的任务", cancellationToken: cts.Token))
{
  Console.Write(token);
}

Console.WriteLine();
```

## CortanaWsClient.cs

```csharp
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

public sealed class CortanaWsClient : IAsyncDisposable
{
  private readonly ClientWebSocket _socket = new();
  private readonly Uri _endpoint;

  public CortanaWsClient(string host, int port)
  {
    _endpoint = new Uri($"ws://{host}:{port}/ws/");
  }

  public async Task ConnectAsync(CancellationToken cancellationToken = default)
  {
    await _socket.ConnectAsync(_endpoint, cancellationToken);
    var connected = await ReceiveMessageAsync(cancellationToken);
    if (connected?.Type != "connected")
      throw new InvalidOperationException("未收到 connected 消息。");
  }

  public async IAsyncEnumerable<string> SendAndStreamAsync(
    string text,
    IReadOnlyList<WsAttachment>? attachments = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    var request = new WsClientMessage
    {
      Type = "send",
      Data = text,
      Attachments = attachments?.ToList()
    };

    await SendMessageAsync(request, cancellationToken);

    while (!cancellationToken.IsCancellationRequested)
    {
      var message = await ReceiveMessageAsync(cancellationToken);
      if (message is null)
        yield break;

      if (message.Type == "token" && message.Data is not null)
      {
        yield return message.Data;
        continue;
      }

      if (message.Type == "done")
        yield break;

      if (message.Type == "error")
        throw new InvalidOperationException(message.Data ?? "Cortana 返回错误。");
    }
  }

  public Task StopAsync(CancellationToken cancellationToken = default)
    => SendMessageAsync(new WsClientMessage { Type = "stop" }, cancellationToken);

  private async Task SendMessageAsync(WsClientMessage message, CancellationToken cancellationToken)
  {
    var json = JsonSerializer.Serialize(message, WsJsonContext.Default.WsClientMessage);
    var payload = Encoding.UTF8.GetBytes(json);
    await _socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
  }

  private async Task<WsServerMessage?> ReceiveMessageAsync(CancellationToken cancellationToken)
  {
    var buffer = new byte[8192];
    using var stream = new MemoryStream();

    while (true)
    {
      var result = await _socket.ReceiveAsync(buffer, cancellationToken);
      if (result.MessageType == WebSocketMessageType.Close)
        return null;

      stream.Write(buffer, 0, result.Count);
      if (result.EndOfMessage)
        break;
    }

    return JsonSerializer.Deserialize(stream.ToArray(), WsJsonContext.Default.WsServerMessage);
  }

  public async ValueTask DisposeAsync()
  {
    if (_socket.State == WebSocketState.Open)
      await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);

    _socket.Dispose();
  }
}
```

## WsContracts.cs

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

## WsJsonContext.cs

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(WsClientMessage))]
[JsonSerializable(typeof(WsServerMessage))]
[JsonSerializable(typeof(WsAttachment))]
[JsonSerializable(typeof(List<WsAttachment>))]
[JsonSourceGenerationOptions(
  DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
  PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class WsJsonContext : JsonSerializerContext;
```

## 扩展点

- 需要上传附件时，只扩展 Program.cs 的 attachments 列表，不改协议模型。
- 需要 UI 集成时，把 SendAndStreamAsync 的 token 输出改为事件或 Channel。
- 需要更强的断线重连时，在外层调用方控制重建 CortanaWsClient，不在序列化层做动态反射。