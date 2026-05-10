namespace Cortana.Plugins.Memory.Processing;

public interface IMemoryAbstractionService
{
    Task RunAbstractionPassAsync(string? agentId = null, string? workspaceId = null, int minSupportCount = 3, int topPerTopic = 50, CancellationToken cancellationToken = default);
}
