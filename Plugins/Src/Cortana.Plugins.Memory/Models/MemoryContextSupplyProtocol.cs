namespace Cortana.Plugins.Memory.Models;

/// <summary>
/// 长期记忆上下文供应控制面协议常量。
/// </summary>
public static class MemoryContextSupplyProtocol
{
    public const string Protocol = "cortana.plugin-bus";
    public const string Version = "1.0.0";
    public const string SupplyRequestOperation = "memory.context.supply.request";
    public const string SupplyPackageOperation = "memory.context.supply.response";
    public const string SupplyErrorOperation = "memory.context.supply.error";
}
