using System.Reflection;
using System.Runtime.Loader;

namespace Netor.Cortana.Plugin.Dotnet;

/// <summary>
/// 每个 .NET 托管插件目录使用独立的可卸载 <see cref="AssemblyLoadContext"/>。
/// <para>
/// 依赖解析策略：
/// <list type="number">
///   <item>宿主共享程序集（接口契约、运行时库）始终回退到 Default ALC，确保类型统一</item>
///   <item>其余依赖优先从插件目录解析（通过 <see cref="AssemblyDependencyResolver"/>）</item>
///   <item>解析不到时回退到 Default ALC</item>
/// </list>
/// </para>
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// 宿主共享程序集名称前缀。这些程序集始终由 Default ALC 提供，
    /// 避免插件加载自己的副本导致 IPlugin 等接口类型不一致。
    /// </summary>
    private static readonly string[] SharedAssemblyPrefixes =
    [
        "Netor.Cortana.Plugin.Abstractions",
        "Microsoft.Extensions.AI.Abstractions",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Http",
    ];

    public PluginLoadContext(string pluginDllPath)
        : base(name: Path.GetFileNameWithoutExtension(pluginDllPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginDllPath);
    }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 宿主共享程序集始终回退到 Default ALC，确保接口类型统一
        if (IsSharedAssembly(assemblyName.Name))
            return null;

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

        return assemblyPath is not null
            ? LoadFromAssemblyPath(assemblyPath)
            : null; // 回退到 Default ALC
    }

    /// <inheritdoc />
    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        return libraryPath is not null
            ? LoadUnmanagedDllFromPath(libraryPath)
            : nint.Zero;
    }

    private static bool IsSharedAssembly(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        foreach (var prefix in SharedAssemblyPrefixes)
        {
            if (name.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
