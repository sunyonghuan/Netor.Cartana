using System.Text.Json;

using Cortana.Plugins.Memory.Models;

namespace Cortana.Plugins.Memory.Services;

/// <summary>
/// 处理 Memory 插件收到的 PluginBus 消息，并按 type/topic/op 分发到对应业务处理器。
/// </summary>
public sealed class MemoryPluginBusDispatcher(
    MemoryPluginBusConnection pluginBus,
    MemoryConversationEventHandler conversationHandler,
    MemorySupplyRequestHandler supplyRequestHandler)
{
    /// <summary>
    /// 处理一条 PluginBus 文本消息。
    /// </summary>
    /// <param name="text">PluginBus JSON 文本。</param>
    /// <param name="cancellationToken">取消处理的令牌。</param>
    /// <returns>如果消息已被识别并处理，则为 <see langword="true"/>。</returns>
    public async Task<bool> DispatchAsync(string text, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
        var op = root.TryGetProperty("op", out var opElement) ? opElement.GetString() : null;

        if (string.Equals(type, "ping", StringComparison.Ordinal))
        {
            await pluginBus.SendPongAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (string.Equals(type, "response", StringComparison.Ordinal)
            && string.Equals(op, "model.capability.response", StringComparison.Ordinal)
            && pluginBus.TryCompleteModelResponse(text))
        {
            return true;
        }

        if (string.Equals(type, "request", StringComparison.Ordinal)
            && string.Equals(op, MemoryContextSupplyProtocol.SupplyRequestOperation, StringComparison.Ordinal))
        {
            await supplyRequestHandler.HandleAsync(text, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if ((string.Equals(type, "event", StringComparison.Ordinal) || string.Equals(type, "response", StringComparison.Ordinal))
            && conversationHandler.TryHandle(root, op))
        {
            return true;
        }

        return false;
    }
}
