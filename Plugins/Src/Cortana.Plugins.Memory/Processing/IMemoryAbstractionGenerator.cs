using Cortana.Plugins.Memory.Models;

namespace Cortana.Plugins.Memory.Processing;

/// <summary>
/// 抽象记忆生成器接口。
/// </summary>
public interface IMemoryAbstractionGenerator
{
    /// <summary>
    /// 从多个 memory fragment 生成抽象记忆。模型不可用时返回 null，不降级。
    /// </summary>
    Task<MemoryAbstraction?> GenerateAbstractionAsync(string agentId, string? workspaceId, string topic, IReadOnlyList<MemoryFragment> fragments, string traceId, CancellationToken cancellationToken = default);
}
