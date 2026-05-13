namespace Netor.Cortana.Entitys;

/// <summary>
/// Cortana 宿主对外和对内 WebSocket 端点常量。
/// </summary>
public static class CortanaWsEndpoints
{
    public const string PluginBusPath = "/internal";
    public const string ChatPath = PluginBusPath;
    public const string PluginBusProtocol = "cortana.plugin-bus";
    public const string PluginBusVersion = "1.0.0";
    public const string ConversationTopic = "conversation";
    public const string MemoryTopic = "memory";
    public const string ModelTopic = "model";
    public const string PluginTopic = "plugin";

    public const string ConversationEventPublishOperation = "conversation.event.publish";
    public const string ConversationHistoryReplayOperation = "conversation.history.replay";
    public const string ConversationHistoryBatchOperation = "conversation.history.batch";
    public const string ConversationHistoryCompletedOperation = "conversation.history.completed";
    public const string MemoryContextSupplyRequestOperation = "memory.context.supply.request";
    public const string MemoryContextSupplyResponseOperation = "memory.context.supply.response";
    public const string MemoryContextSupplyErrorOperation = "memory.context.supply.error";
    public const string ModelCapabilityRequestOperation = "model.capability.request";
    public const string ModelCapabilityResponseOperation = "model.capability.response";

    public static string BuildChatEndpoint(int port) => BuildPluginBusEndpoint(port);

    public static string BuildPluginBusEndpoint(int port) =>
        $"ws://localhost:{port}{PluginBusPath}";

    public static string BuildModelCapabilityEndpoint(int port) =>
        BuildPluginBusEndpoint(port);
}