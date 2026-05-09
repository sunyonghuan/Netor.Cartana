using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Netor.Cortana.Plugin.Native;

namespace Cortana.Plugins.Memory.Services;

/// <summary>
/// Memory 插件访问宿主大模型能力的轻量客户端。
/// </summary>
public sealed class HostModelCapabilityClient(PluginSettings settings, ILogger<HostModelCapabilityClient> logger)
{
    private const string FallbackPluginId = "memory_engine";

    /// <summary>
    /// 调用宿主授权的大模型能力并返回模型输出文本。
    /// </summary>
    /// <param name="purpose">调用目的。</param>
    /// <param name="instruction">任务指令。</param>
    /// <param name="input">业务输入。</param>
    /// <param name="outputFormat">期望输出格式。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>宿主大模型返回的文本；不可用或失败时返回 <see langword="null"/>。</returns>
    public async Task<string?> InvokeAsync(
        string purpose,
        string instruction,
        string input,
        string outputFormat,
        CancellationToken cancellationToken = default)
    {
        var endpoint = ResolveEndpoint();
        if (string.IsNullOrWhiteSpace(endpoint)) return null;

        try
        {
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(endpoint), cancellationToken);
            _ = await ReadTextMessageAsync(socket, cancellationToken); // connected

            var request = new HostModelCapabilityRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                PluginId = ResolvePluginId(),
                Purpose = purpose,
                Instruction = instruction,
                Input = input,
                OutputFormat = outputFormat,
                TimeoutMs = 30000,
                MaxOutputTokens = 4096
            };

            var json = JsonSerializer.Serialize(request, MemoryHostModelCapabilityJsonContext.Default.HostModelCapabilityRequest);
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);

            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var text = await ReadTextMessageAsync(socket, cancellationToken);
                if (string.IsNullOrWhiteSpace(text)) return null;

                var response = JsonSerializer.Deserialize(text, MemoryHostModelCapabilityJsonContext.Default.HostModelCapabilityResponse);
                if (response is null)
                {
                    return null;
                }

                if (response.Success) return response.Content;

                logger.LogWarning("宿主模型能力调用失败：{Code} {Message}", response.ErrorCode, response.ErrorMessage);
                return null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "宿主模型能力不可用，使用降级记忆处理。 ");
        }

        return null;
    }

    /// <summary>
    /// 解析当前插件标识，优先使用宿主下发扩展配置，其次读取调试或发布目录中的 <c>plugin.json</c>。
    /// </summary>
    /// <returns>插件标识。</returns>
    private string ResolvePluginId()
    {
        if (settings.Extensions.TryGetValue("pluginId", out var extensionPluginId)
            && !string.IsNullOrWhiteSpace(extensionPluginId))
        {
            return extensionPluginId;
        }

        var manifestPath = Path.Combine(AppContext.BaseDirectory, "plugin.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                var json = File.ReadAllText(manifestPath);
                var metadata = JsonSerializer.Deserialize(json, MemoryHostModelCapabilityJsonContext.Default.HostPluginManifestMetadata);
                if (!string.IsNullOrWhiteSpace(metadata?.Id)) return metadata.Id;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "读取插件清单失败，使用默认插件 ID。Path={Path}", manifestPath);
            }
        }

        return FallbackPluginId;
    }

    /// <summary>
    /// 从宿主下发的插件扩展配置中解析模型能力控制面地址。
    /// </summary>
    /// <returns>WebSocket 地址；未下发时返回 <see langword="null"/>。</returns>
    private string? ResolveEndpoint()
    {
        if (settings.Extensions.TryGetValue("modelCapabilityEndpoint", out var endpoint)
            && !string.IsNullOrWhiteSpace(endpoint))
        {
            return endpoint;
        }

        if (settings.Extensions.TryGetValue("modelCapabilityPort", out var portValue)
            && int.TryParse(portValue, out var port)
            && port > 0)
        {
            return $"ws://localhost:{port}/internal/model-capability/";
        }

        if (settings.ConversationFeedPort > 0)
        {
            return $"ws://localhost:{settings.ConversationFeedPort}/internal/model-capability/";
        }

        return null;
    }

    /// <summary>
    /// 从 WebSocket 读取一条完整文本消息。
    /// </summary>
    /// <param name="socket">WebSocket 连接。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>文本消息；连接关闭时返回 <see langword="null"/>。</returns>
    private static async Task<string?> ReadTextMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.Count > 0) message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }

        return Encoding.UTF8.GetString(message.ToArray());
    }
}
