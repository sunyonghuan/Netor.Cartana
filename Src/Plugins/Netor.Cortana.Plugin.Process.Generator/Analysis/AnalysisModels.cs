using System.Collections.Generic;

using Microsoft.CodeAnalysis;

namespace Netor.Cortana.Plugin.Process.Generator.Analysis;

/// <summary>插件入口类的分析结果。</summary>
internal sealed class PluginClassInfo
{
    public INamedTypeSymbol ClassSymbol { get; }
    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string Description { get; }
    public string[] Tags { get; }
    public string? Instructions { get; }
    public string? Namespace { get; }
    public string ClassName { get; }

    /// <summary>Configure 方法是否存在（可选：用户可以不提供）。</summary>
    public bool HasConfigureMethod { get; }

    public PluginClassInfo(
        INamedTypeSymbol classSymbol,
        string id,
        string name,
        string version,
        string description,
        string[] tags,
        string? instructions,
        bool hasConfigureMethod)
    {
        ClassSymbol = classSymbol;
        Id = id;
        Name = name;
        Version = version;
        Description = description;
        Tags = tags;
        Instructions = instructions;
        HasConfigureMethod = hasConfigureMethod;
        Namespace = classSymbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : classSymbol.ContainingNamespace?.ToDisplayString();
        ClassName = classSymbol.Name;
    }
}

/// <summary>工具类的分析结果。</summary>
internal sealed class ToolClassInfo
{
    public INamedTypeSymbol ClassSymbol { get; }
    public string ClassName { get; }
    public List<ToolMethodInfo> Methods { get; }

    public ToolClassInfo(INamedTypeSymbol classSymbol, List<ToolMethodInfo> methods)
    {
        ClassSymbol = classSymbol;
        ClassName = classSymbol.Name;
        Methods = methods;
    }
}

/// <summary>工具方法的分析结果。</summary>
internal sealed class ToolMethodInfo
{
    public string ClassName { get; }
    public string MethodName { get; }
    public string FullToolName { get; }
    public string MethodSnakeName { get; }
    public string Description { get; }
    public List<ToolParamInfo> Parameters { get; }
    public ITypeSymbol ReturnType { get; }
    public bool IsAsync { get; }
    public ITypeSymbol? AsyncInnerType { get; }
    public bool IsValueTask { get; }
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

/// <summary>工具方法参数的分析结果。</summary>
internal sealed class ToolParamInfo
{
    public string ParamName { get; }
    public string JsonName { get; }
    public string Description { get; }
    public bool Required { get; }
    public string JsonType { get; }
    public ITypeSymbol TypeSymbol { get; }
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
