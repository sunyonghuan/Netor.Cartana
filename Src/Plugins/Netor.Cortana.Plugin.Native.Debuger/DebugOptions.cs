using Microsoft.Extensions.DependencyInjection;

namespace Netor.Cortana.Plugin.Native.Debugger;

/// <summary>
/// 调试运行选项
/// </summary>
public sealed class DebugOptions
{
    public string? DataDirectory { get; set; }
    public string? WorkspaceDirectory { get; set; }
    public string? PluginDirectory { get; set; }
    public int WsPort { get; set; } = 9090;
    public Action<IServiceCollection>? ConfigureServices { get; set; }
}
