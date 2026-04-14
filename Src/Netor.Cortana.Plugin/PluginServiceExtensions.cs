using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Netor.Cortana.Plugin.BuiltIn.ApplicationLauncher;
using Netor.Cortana.Plugin.BuiltIn.FileBrowser;
using Netor.Cortana.Plugin.BuiltIn.PowerShell;
using Netor.Cortana.Plugin.BuiltIn.WindowManagement;

namespace Netor.Cortana.Plugin;

/// <summary>
/// Plugin 模块的 DI 扩展方法
/// </summary>
public static class PluginServiceExtensions
{
    /// <summary>
    /// 注册 Plugin 模块的所有服务
    /// </summary>
    public static IServiceCollection AddCortanaPlugin(this IServiceCollection services)
    {
        services.AddSingleton<PluginLoader>();

        // 内置 Provider（后续 AOT 插件化后移除）
        services.AddSingleton<AIContextProvider, PowerShellProvider>();
        services.AddSingleton<AIContextProvider, WindowManagerProvider>();
        services.AddSingleton<AIContextProvider, FileBrowserProvider>();
        services.AddSingleton<AIContextProvider, FileOperationProvider>();
        services.AddSingleton<AIContextProvider, ApplicationLauncherProvider>();

        // 辅助服务
        services.AddSingleton<PowerShellExecutor>();
        services.AddSingleton<PowerShellOutputBridge>();
        services.AddSingleton<SessionRegistry>();
        services.AddSingleton<WindowManager>();
        services.AddSingleton<FileBrowser>();
        services.AddSingleton<FileOperator>();
        services.AddSingleton<ApplicationLauncher>();

        return services;
    }
}
