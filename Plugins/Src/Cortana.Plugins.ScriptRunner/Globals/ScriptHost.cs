namespace Cortana.Plugins.ScriptRunner.Globals;

/// <summary>
/// <see cref="IScriptHost"/> 的默认实现。
/// </summary>
internal sealed class ScriptHost : IScriptHost
{
    public string PluginDirectory { get; init; } = string.Empty;
    public string DataDirectory { get; init; } = string.Empty;
    public string WorkspaceDirectory { get; init; } = string.Empty;
    public CancellationToken Cancellation { get; set; }
}
