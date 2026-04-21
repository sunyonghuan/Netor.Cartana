namespace Netor.Cortana.Plugin;

/// <summary>
/// 统一标记工具类和工具方法。
/// <para>
/// <b>标记在类上</b> → 声明这是一个工具类（等效于类名以 <c>Tools</c> 结尾的约定）。<br/>
/// <b>标记在方法上</b> → 声明这是一个工具方法，提供描述和自定义名称。
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ToolAttribute : Attribute
{
    /// <summary>
    /// 标记在方法上时：工具名称。不填则从方法名自动转换 PascalCase → snake_case。
    /// 标记在类上时：忽略。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 标记在方法上时：工具描述，告诉 AI 这个工具做什么。
    /// 标记在类上时：工具类的分组描述（可选）。
    /// </summary>
    public string Description { get; init; } = "";
}
