namespace Netor.Cortana.Plugin;

/// <summary>
/// 标记插件入口类。一个项目有且只有一个。
/// <para>
/// Generator 会从此 Attribute 提取元数据，生成：
/// <list type="bullet">
///   <item>Native 通道：C 导出函数 + plugin.json</item>
///   <item>Process 通道：Program.g.cs 消息循环入口 + plugin.json + 强类型 Debugger</item>
/// </list>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PluginAttribute : Attribute
{
    /// <summary>
    /// 插件唯一标识。
    /// <para>仅允许小写字母、数字和下划线（如 <c>com_netor_weather</c>）。</para>
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 插件名称。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 插件版本。
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// 插件描述。
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// 分类标签。
    /// </summary>
    public string[] Tags { get; init; } = [];

    /// <summary>
    /// AI 系统指令片段，告诉 AI 什么时候使用这些工具、怎么用。
    /// </summary>
    public string? Instructions { get; init; }
}
