using System.Reflection;

using Microsoft.Extensions.Logging;

using Netor.Cortana.Plugin;

namespace Netor.Cortana.Plugin.Dotnet;

/// <summary>
/// .NET 托管通道的插件宿主。
/// 使用独立的 <see cref="PluginLoadContext"/> 加载插件程序集，
/// 扫描所有 <see cref="IPlugin"/> 实现并初始化。
/// </summary>
public sealed class DotNetPluginHost : IDisposable
{
    private readonly ILogger<DotNetPluginHost> _logger;
    private readonly List<IPlugin> _plugins = [];
    private PluginLoadContext? _loadContext;
    private bool _disposed;

    /// <summary>已加载的插件实例列表。</summary>
    public IReadOnlyList<IPlugin> Plugins => _plugins;

    /// <summary>插件目录路径。</summary>
    public string PluginDirectory { get; }

    /// <summary>插件清单。</summary>
    public PluginManifest Manifest { get; }

    public DotNetPluginHost(string pluginDirectory, PluginManifest manifest, ILogger<DotNetPluginHost> logger)
    {
        PluginDirectory = pluginDirectory;
        Manifest = manifest;
        _logger = logger;
    }

    /// <summary>
    /// 加载插件程序集，扫描并初始化所有 <see cref="IPlugin"/> 实现。
    /// </summary>
    public async Task LoadAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dllPath = Path.Combine(PluginDirectory, Manifest.AssemblyName!);

        if (!File.Exists(dllPath))
        {
            _logger.LogWarning("插件程序集不存在：{DllPath}", dllPath);
            return;
        }

        try
        {
            _loadContext = new PluginLoadContext(dllPath);
            var assembly = _loadContext.LoadFromAssemblyPath(dllPath);
            var pluginTypes = ScanPluginTypes(assembly);

            if (pluginTypes.Count == 0)
            {
                _logger.LogWarning("程序集 {Assembly} 中未找到 IPlugin 实现", Manifest.AssemblyName);
                return;
            }

            foreach (var pluginType in pluginTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (Activator.CreateInstance(pluginType) is not IPlugin plugin)
                    {
                        _logger.LogWarning("无法实例化插件类型：{Type}", pluginType.FullName);
                        continue;
                    }

                    await plugin.InitializeAsync(context);
                    _plugins.Add(plugin);

                    _logger.LogInformation(
                        "已加载插件：{Id} ({Name} v{Version})，工具数：{ToolCount}",
                        plugin.Id, plugin.Name, plugin.Version, plugin.Tools.Count);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "初始化插件类型 {Type} 失败", pluginType.FullName);
                }
            }
        }
        catch (BadImageFormatException ex)
        {
            // AOT 编译的 DLL 不包含 IL，应走 native 通道
            _logger.LogWarning(ex,
                "程序集 {Assembly} 不是有效的 IL 格式（可能是 AOT 编译），应使用 native 运行时模式",
                Manifest.AssemblyName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "加载插件目录 {Dir} 失败", PluginDirectory);
        }
    }

    /// <summary>
    /// 卸载所有插件并释放 ALC。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var plugin in _plugins)
        {
            try
            {
                if (plugin is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放插件 {Id} 时出错", plugin.Id);
            }
        }

        _plugins.Clear();

        // 卸载 ALC，GC 回收后程序集释放
        _loadContext?.Unload();
        _loadContext = null;
    }

    /// <summary>
    /// 扫描程序集中所有实现 <see cref="IPlugin"/> 的公共非抽象类。
    /// </summary>
    private List<Type> ScanPluginTypes(Assembly assembly)
    {
        var pluginInterfaceType = typeof(IPlugin);
        List<Type> result = [];

        try
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                if (type is { IsClass: true, IsAbstract: false }
                    && pluginInterfaceType.IsAssignableFrom(type))
                {
                    result.Add(type);
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            _logger.LogWarning(ex, "扫描程序集类型时部分加载失败");

            foreach (var type in ex.Types)
            {
                if (type is { IsClass: true, IsAbstract: false }
                    && pluginInterfaceType.IsAssignableFrom(type))
                {
                    result.Add(type);
                }
            }
        }

        return result;
    }
}
