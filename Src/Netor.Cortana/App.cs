using Microsoft.Extensions.Hosting;

using Netor.Cortana.Networks;
using Netor.Cortana.Plugin;

using System.Collections.Specialized;

namespace Netor.Cortana;

internal partial class App : AppStartup
{
    private ILogger<App> logger => App.Services.GetRequiredService<ILogger<App>>();
    public static CancellationTokenSource CancellationTokenSource { get; private set; } = new();

    private static FloatWindow? _floatWindow;
    private static WakeWordBubbleWindow? _bubbleWindow;

    /// <summary>
    /// 应用启动时调用，返回主窗口。
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    protected override AppCreationAction? OnApplicationStartup(StartupSettings settings)
    {
        var main = App.Services.GetRequiredService<MainWindow>();
        _floatWindow = App.Services.GetRequiredService<FloatWindow>();
        _bubbleWindow = App.Services.GetRequiredService<WakeWordBubbleWindow>();
        _bubbleWindow.SetAnchorWindow(_floatWindow);
        var app = settings.UseMainWindow(main);
        _floatWindow.Show();
        App.MainWindow = main;
        var cortanaPath = Path.Combine(App.WorkspaceDirectory, ".cortana");
        if (!Directory.Exists(cortanaPath))
            Directory.CreateDirectory(cortanaPath);
        if (!Directory.Exists(App.UserSkillsDirectory))
            Directory.CreateDirectory(App.UserSkillsDirectory);
        if (!Directory.Exists(App.UserPluginsDirectory))
            Directory.CreateDirectory(App.UserPluginsDirectory);
        if (!Directory.Exists(App.PluginDirectory))
            Directory.CreateDirectory(App.PluginDirectory);

        // 从数据库读取工作目录，路径不存在时回退到 UserDataDirectory
        var sysSettings = App.Services.GetRequiredService<SystemSettingsService>();
        var savedWorkspace = sysSettings.GetValue("System.WorkspaceDirectory");
        var workspacePath = (!string.IsNullOrWhiteSpace(savedWorkspace) && Directory.Exists(savedWorkspace))
            ? savedWorkspace
            : App.UserDataDirectory;
        App.ChangeWorkspaceDirectory(workspacePath);

        return app;
    }

    // 应用启动后调用
    protected override bool OnApplicationLaunched(string[] commandArgs)
    {
        // 浮动窗口位置变更 → 气泡窗口跟随移动
        _floatWindow?.PositionChanged += () => _bubbleWindow?.OnAnchorMoved();

        _ = Task.Run(async () =>
        {
            var pluginLogger = App.Services.GetRequiredService<ILogger<App>>();
            try
            {
                await App.StartBackgroudServicesAsync(App.CancellationTokenSource.Token);

                var wsServer = App.Services.GetRequiredService<WebSocketServerService>();
                var pluginLoader = App.Services.GetRequiredService<PluginLoader>();
                pluginLoader.WsPort = wsServer.Port;

                if (pluginLoader.WsPort <= 0)
                {
                    pluginLogger.LogWarning("WebSocket 服务器端口未初始化，插件可能无法连接宿主：{Port}", pluginLoader.WsPort);
                }

                await pluginLoader.ScanAndLoadAsync(App.CancellationTokenSource.Token);
                pluginLoader.StartWatching();

                // 加载已启用的 MCP 服务器
                var mcpService = App.Services.GetRequiredService<McpServerService>();
                await pluginLoader.LoadMcpServersAsync(mcpService, App.CancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                pluginLogger.LogError(ex, "插件/MCP 系统启动失败");
            }
        });

        return true;
    }

    // 应用关闭时调用
    protected override void OnApplicationTerminated()
    {
        _ = App.StopBackgroudServicesAsync(App.CancellationTokenSource.Token);
        App.CancellationTokenSource.Cancel();
        // 清理资源
    }

    // 未处理异常回调
    protected override void OnApplicationException(Exception? exception = null)
    {
        logger.LogError(exception, "应用程序发生异常");
    }

    protected override void ConfigureAdditionalBrowserArgs(NameValueCollection additionalBrowserArgs)
    {
        base.ConfigureAdditionalBrowserArgs(additionalBrowserArgs);
    }

    /// <summary>
    /// 启动背景服务
    /// </summary>
    /// <returns></returns>
    private static async Task StartBackgroudServicesAsync(CancellationToken cancellationToken = default)
    {
        var logger = App.Services.GetRequiredService<ILogger<App>>();
        try
        {
            var services = App.Services.GetServices<IHostedService>();

            foreach (var service in services)
                try
                {
                    await service.StartAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "启动服务失败:{service}", service.ToString());
                }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "启动服务失败");
        }
    }

    private static async Task StopBackgroudServicesAsync(CancellationToken cancellationToken = default)
    {
        var services = App.Services.GetServices<IHostedService>();
        var logger = App.Services.GetRequiredService<ILogger<App>>();
        foreach (var service in services)
            try
            {
                await service.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "停止服务失败:{service}", service.ToString());
            }
    }

    }