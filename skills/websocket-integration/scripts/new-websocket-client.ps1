param(
    [Parameter(Mandatory = $true)][string]$OutputDir,
    [string]$ProjectName = 'CortanaWsClientSample'
)

$ErrorActionPreference = 'Stop'

$fullOutputDir = [System.IO.Path]::GetFullPath($OutputDir)
if (Test-Path $fullOutputDir) {
    throw "输出目录已存在：$fullOutputDir"
}

New-Item -ItemType Directory -Path $fullOutputDir -Force | Out-Null

$csproj = @"
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
"@

$program = @"
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var client = new ClientWebSocket();
var uri = new Uri("ws://localhost:52841/ws/");
await client.ConnectAsync(uri, CancellationToken.None);

var send = new WsClientMessage
{
    Type = "send",
    Data = "你好，Cortana"
};

var sendJson = JsonSerializer.Serialize(send, WsJsonContext.Default.WsClientMessage);
await client.SendAsync(Encoding.UTF8.GetBytes(sendJson), WebSocketMessageType.Text, true, CancellationToken.None);

var buffer = new byte[8192];
while (client.State == WebSocketState.Open)
{
    var result = await client.ReceiveAsync(buffer, CancellationToken.None);
    if (result.MessageType == WebSocketMessageType.Close)
        break;

    var message = JsonSerializer.Deserialize(buffer.AsSpan(0, result.Count), WsJsonContext.Default.WsServerMessage);
    Console.WriteLine($"[{message?.Type}] {message?.Data}");
    if (message?.Type is "done" or "error")
        break;
}

await client.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);

public sealed record WsClientMessage
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("data")] public string? Data { get; init; }
}

public sealed record WsServerMessage
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("data")] public string? Data { get; init; }
    [JsonPropertyName("clientId")] public string? ClientId { get; init; }
    [JsonPropertyName("sessionId")] public string? SessionId { get; init; }
}

[JsonSerializable(typeof(WsClientMessage))]
[JsonSerializable(typeof(WsServerMessage))]
internal partial class WsJsonContext : JsonSerializerContext;
"@

Set-Content -Path (Join-Path $fullOutputDir "$ProjectName.csproj") -Value $csproj -Encoding UTF8
Set-Content -Path (Join-Path $fullOutputDir 'Program.cs') -Value $program -Encoding UTF8

Write-Host "已生成 WebSocket 客户端样板：$fullOutputDir" -ForegroundColor Green