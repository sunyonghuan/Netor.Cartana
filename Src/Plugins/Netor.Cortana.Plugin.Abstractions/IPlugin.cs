using Microsoft.Extensions.AI;

namespace Netor.Cortana.Plugin;

/// <summary>
/// 插件的统一接口。Native 插件通过 NativePluginWrapper 适配，
/// Dotnet 插件直接实现此接口，Process 插件通过 ProcessPluginWrapper 适配。
/// </summary>
public interface IPlugin
{
    /// <summary>插件的唯一标识符（小写字母、数字、下划线）。</summary>
    string Id { get; }

    /// <summary>插件的显示名称。</summary>
    string Name { get; }

    /// <summary>插件版本。</summary>
    Version Version { get; }

    /// <summary>插件功能描述。</summary>
    string Description { get; }

    /// <summary>AI 指令（可选，供 AI 模型理解插件的用途）。</summary>
    string? Instructions { get; }

    /// <summary>插件的分类标签。</summary>
    IReadOnlyList<string> Tags { get; }

    /// <summary>插件暴露的所有工具。</summary>
    IReadOnlyList<AITool> Tools { get; }

    /// <summary>
    /// 插件初始化方法。
    /// 宿主在加载插件后立即调用，插件可在此方法中初始化资源、连接数据库等。
    /// </summary>
    /// <param name="context">宿主提供的运行时上下文。</param>
    /// <returns>初始化完成后的 Task。</returns>
    Task InitializeAsync(IPluginContext context);
}
