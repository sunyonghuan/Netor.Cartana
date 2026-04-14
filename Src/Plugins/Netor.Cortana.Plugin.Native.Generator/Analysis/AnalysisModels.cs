using System.Collections.Generic;

using Microsoft.CodeAnalysis;

namespace Netor.Cortana.Plugin.Native.Generator.Analysis;

/// <summary>
/// 插件入口类的分析结果。
/// </summary>
internal sealed class PluginClassInfo
{
    /// <summary>插件入口类的符号。</summary>
    public INamedTypeSymbol ClassSymbol { get; }

    /// <summary>插件 Id。</summary>
    public string Id { get; }

    /// <summary>插件名称。</summary>
    public string Name { get; }

    /// <summary>插件版本。</summary>
    public string Version { get; }

    /// <summary>插件描述。</summary>
    public string Description { get; }

    /// <summary>分类标签。</summary>
    public string[] Tags { get; }

    /// <summary>AI 指令。</summary>
    public string? Instructions { get; }

    /// <summary>插件入口类所在的命名空间。</summary>
    public string? Namespace { get; }

    /// <summary>插件入口类名。</summary>
    public string ClassName { get; }

    public PluginClassInfo(
        INamedTypeSymbol classSymbol,
        string id,
        string name,
        string version,
        string description,
        string[] tags,
        string? instructions)
    {
        ClassSymbol = classSymbol;
        Id = id;
        Name = name;
        Version = version;
        Description = description;
        Tags = tags;
        Instructions = instructions;
        Namespace = classSymbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : classSymbol.ContainingNamespace?.ToDisplayString();
        ClassName = classSymbol.Name;
    }
}

/// <summary>
/// 工具类的分析结果。
/// </summary>
internal sealed class ToolClassInfo
{
    /// <summary>工具类的符号。</summary>
    public INamedTypeSymbol ClassSymbol { get; }

    /// <summary>工具类名。</summary>
    public string ClassName { get; }

    /// <summary>工具类中的工具方法列表。</summary>
    public List<ToolMethodInfo> Methods { get; }

    public ToolClassInfo(
        INamedTypeSymbol classSymbol,
        List<ToolMethodInfo> methods)
    {
        ClassSymbol = classSymbol;
        ClassName = classSymbol.Name;
        Methods = methods;
    }
}

/// <summary>
/// 工具方法的分析结果。
/// </summary>
internal sealed class ToolMethodInfo
{
    /// <summary>所属工具类名。</summary>
    public string ClassName { get; }

    /// <summary>方法名。</summary>
    public string MethodName { get; }

    /// <summary>生成的完整工具名（含 plugin_id 前缀）。</summary>
    public string FullToolName { get; }

    /// <summary>方法级 snake_case 名称（不含前缀）。</summary>
    public string MethodSnakeName { get; }

    /// <summary>工具描述。</summary>
    public string Description { get; }

    /// <summary>参数列表。</summary>
    public List<ToolParamInfo> Parameters { get; }

    /// <summary>返回类型符号。</summary>
    public ITypeSymbol ReturnType { get; }

    /// <summary>是否异步方法。</summary>
    public bool IsAsync { get; }

    /// <summary>异步方法的内部返回类型（Task&lt;T&gt; 的 T）。</summary>
    public ITypeSymbol? AsyncInnerType { get; }

    /// <summary>是否为 ValueTask。</summary>
    public bool IsValueTask { get; }

    /// <summary>方法符号。</summary>
    public IMethodSymbol MethodSymbol { get; }

    public ToolMethodInfo(
        string className,
        string methodName,
        string fullToolName,
        string methodSnakeName,
        string description,
        List<ToolParamInfo> parameters,
        ITypeSymbol returnType,
        bool isAsync,
        ITypeSymbol? asyncInnerType,
        bool isValueTask,
        IMethodSymbol methodSymbol)
    {
        ClassName = className;
        MethodName = methodName;
        FullToolName = fullToolName;
        MethodSnakeName = methodSnakeName;
        Description = description;
        Parameters = parameters;
        ReturnType = returnType;
        IsAsync = isAsync;
        AsyncInnerType = asyncInnerType;
        IsValueTask = isValueTask;
        MethodSymbol = methodSymbol;
    }
}

/// <summary>
/// 工具方法参数的分析结果。
/// </summary>
internal sealed class ToolParamInfo
{
    /// <summary>参数名（方法参数名或 [ParameterAttribute] 指定）。</summary>
    public string ParamName { get; }

    /// <summary>生成的参数名（snake_case）。</summary>
    public string JsonName { get; }

    /// <summary>参数描述。</summary>
    public string Description { get; }

    /// <summary>是否必填。</summary>
    public bool Required { get; }

    /// <summary>JSON Schema 类型。</summary>
    public string JsonType { get; }

    /// <summary>参数类型符号。</summary>
    public ITypeSymbol TypeSymbol { get; }

    /// <summary>原始方法参数名（代码中使用）。</summary>
    public string CodeParamName { get; }

    public ToolParamInfo(
        string paramName,
        string jsonName,
        string description,
        bool required,
        string jsonType,
        ITypeSymbol typeSymbol,
        string codeParamName)
    {
        ParamName = paramName;
        JsonName = jsonName;
        Description = description;
        Required = required;
        JsonType = jsonType;
        TypeSymbol = typeSymbol;
        CodeParamName = codeParamName;
    }
}
