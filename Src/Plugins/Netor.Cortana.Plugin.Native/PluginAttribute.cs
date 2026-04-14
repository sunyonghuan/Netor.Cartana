namespace Netor.Cortana.Plugin.Native;

/// <summary>
/// 标记 Native 插件入口类。一个项目有且只有一个。
/// <para>
/// 对应 <c>IPlugin</c> 接口的所有元数据属性，
/// Generator 会从此 Attribute 提取信息生成导出函数和 plugin.json。
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PluginAttribute : Attribute
{
    /// <summary>
    /// 插件唯一标识。对应 IPlugin.Id。
    /// <para>仅允许小写字母、数字和下划线（如 <c>com_netor_native_test</c>）。</para>
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 插件名称。对应 IPlugin.Name。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 插件版本。对应 IPlugin.Version。
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// 插件描述。对应 IPlugin.Description。
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// 分类标签。对应 IPlugin.Tags。
    /// </summary>
    public string[] Tags { get; init; } = [];

    /// <summary>
    /// AI 系统指令片段。对应 IPlugin.Instructions。
    /// 告诉 AI 什么时候使用这些工具、怎么用。
    /// </summary>
    public string? Instructions { get; init; }
}