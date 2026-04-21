namespace Netor.Cortana.Plugin;

/// <summary>
/// 标记工具方法参数的描述信息。
/// <para>
/// Generator 会从此 Attribute 提取参数名称、描述和是否必填，
/// 生成到插件元数据（plugin.json / get_info 响应）中。
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class ParameterAttribute : Attribute
{
    /// <summary>
    /// 参数名称。不填则使用方法参数名（自动转换为 snake_case）。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 参数描述，告诉 AI 这个参数的含义。
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// 是否必填。默认 <c>true</c>。
    /// 值类型始终为 <c>true</c>，引用类型根据可空性推断。
    /// </summary>
    public bool Required { get; init; } = true;
}
