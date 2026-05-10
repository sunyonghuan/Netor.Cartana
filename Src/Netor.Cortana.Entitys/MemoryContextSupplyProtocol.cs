namespace Netor.Cortana.Entitys;

/// <summary>
/// 长期记忆上下文供应控制面协议常量。
/// </summary>
public static class MemoryContextSupplyProtocol
{
    public const string Protocol = "memory-context-supply";
    public const string Version = "1.0.0";
    public const string SupplyRequestOperation = "memory.supply.request";
    public const string SupplyPackageOperation = "memory.supply.package";
    public const string SupplyErrorOperation = "memory.supply.error";
}
