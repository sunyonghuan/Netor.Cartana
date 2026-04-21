using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Netor.Cortana.Plugin.Native.Generator.Diagnostics;

namespace Netor.Cortana.Plugin.Native.Generator.Analysis;

/// <summary>
/// 全项目扫描所有标记了 [Tool] 的类，提取工具方法信息。
/// </summary>
internal static class ToolClassAnalyzer
{
    private const string ToolAttributeName = "Netor.Cortana.Plugin.ToolAttribute";
    private const string ParameterAttributeName = "Netor.Cortana.Plugin.ParameterAttribute";

    /// <summary>
    /// 扫描编译上下文中所有标记了 [Tool] 的类，提取工具类和工具方法信息。
    /// 同时检测含有 [Tool] 方法但类上未标记 [Tool] 的情况（CNPG005）。
    /// </summary>
    public static List<ToolClassInfo> ScanAll(
        Compilation compilation,
        string pluginId,
        Action<Diagnostic> reportDiagnostic)
    {
        var result = new List<ToolClassInfo>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var classSyntax in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classSyntax) as INamedTypeSymbol;
                if (classSymbol == null)
                    continue;

                var hasToolAttrOnClass = classSymbol.GetAttributes()
                    .Any(a => a.AttributeClass?.ToDisplayString() == ToolAttributeName);

                // 检查类中是否有任何方法标记了 [Tool]
                var hasToolMethods = classSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Any(m => m.GetAttributes()
                        .Any(a => a.AttributeClass?.ToDisplayString() == ToolAttributeName));

                if (!hasToolAttrOnClass && !hasToolMethods)
                    continue;

                // CNPG005: 含有 [Tool] 方法但类上未标记 [Tool]
                if (!hasToolAttrOnClass && hasToolMethods)
                {
                    reportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ToolMethodWithoutToolClass,
                        classSyntax.Identifier.GetLocation(),
                        classSymbol.Name));
                    continue;
                }

                var toolClassInfo = AnalyzeToolClass(classSymbol, classSyntax, pluginId, reportDiagnostic);
                if (toolClassInfo != null)
                    result.Add(toolClassInfo);
            }
        }

        return result;
    }

    /// <summary>
    /// 分析单个 [Tool] 类，验证约束并提取工具方法。
    /// </summary>
    private static ToolClassInfo? AnalyzeToolClass(
        INamedTypeSymbol classSymbol,
        ClassDeclarationSyntax classSyntax,
        string pluginId,
        Action<Diagnostic> reportDiagnostic)
    {
        // CNPG005: 工具类必须是 public
        if (classSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NotPublicClass,
                classSyntax.Identifier.GetLocation(),
                classSymbol.Name));
            return null;
        }

        var methods = new List<ToolMethodInfo>();

        foreach (var member in classSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var toolAttr = member.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ToolAttributeName);

            if (toolAttr == null)
                continue;

            var methodInfo = AnalyzeMethod(member, classSymbol, pluginId, toolAttr, reportDiagnostic);
            if (methodInfo != null)
                methods.Add(methodInfo);
        }

        // CNPG005: [Tool] 类中没有任何 [Tool] 方法
        if (methods.Count == 0)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NotPublicClass,
                classSyntax.Identifier.GetLocation(),
                classSymbol.Name));
            return null;
        }

        return new ToolClassInfo(classSymbol, methods);
    }

    /// <summary>
    /// 分析单个 [Tool] 方法，生成工具名并提取参数信息。
    /// </summary>
    private static ToolMethodInfo? AnalyzeMethod(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol classSymbol,
        string pluginId,
        AttributeData toolAttr,
        Action<Diagnostic> reportDiagnostic)
    {
        // CNPG006: 方法必须是 public
        if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NotPublicMethod,
                methodSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                methodSymbol.Name));
            return null;
        }

        // 解析 [Tool] 属性中的 Name 和 Description
        var namedArgs = toolAttr.NamedArguments.ToDictionary(kv => kv.Key, kv => kv.Value);
        string? customName = null;
        if (namedArgs.TryGetValue("Name", out var nameValue) && nameValue.Value is string n)
            customName = n;

        string description = "";
        if (namedArgs.TryGetValue("Description", out var descValue) && descValue.Value is string d)
            description = d;

        // 生成 method snake name
        string methodSnakeName;
        if (!string.IsNullOrEmpty(customName))
        {
            methodSnakeName = customName;
        }
        else
        {
            // CNPG007: 方法名已经是 snake_case（警告）
            if (TypeMapper.IsSnakeCase(methodSymbol.Name))
            {
                reportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AlreadySnakeCase,
                    methodSymbol.Locations.FirstOrDefault(),
                    methodSymbol.Name));
            }

            methodSnakeName = TypeMapper.ToSnakeCase(methodSymbol.Name);
        }

        // 生成完整工具名（两段式：{pluginId}_{methodSnake}）
        var fullToolName = ToolNameGenerator.GenerateFullName(pluginId, methodSnakeName);

        // CNPG008: 工具名无效
        if (!ToolNameGenerator.IsValidToolNamePart(methodSnakeName))
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidToolName,
                methodSymbol.Locations.FirstOrDefault(),
                methodSymbol.Name,
                methodSnakeName));
            return null;
        }

        // 分析参数
        var parameters = new List<ToolParamInfo>();
        bool hasInvalidParam = false;

        foreach (var param in methodSymbol.Parameters)
        {
            var paramInfo = AnalyzeParameter(param, classSymbol.Name, methodSymbol.Name, reportDiagnostic);
            if (paramInfo == null)
            {
                hasInvalidParam = true;
                continue;
            }
            parameters.Add(paramInfo);
        }

        if (hasInvalidParam)
            return null;

        // 解析返回类型
        var (isAsync, asyncInnerType) = TypeMapper.UnwrapAsync(methodSymbol.ReturnType);
        bool isValueTask = false;
        if (isAsync && methodSymbol.ReturnType is INamedTypeSymbol namedReturnType)
        {
            var fullName = namedReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            isValueTask = fullName.StartsWith("global::System.Threading.Tasks.ValueTask");
        }

        return new ToolMethodInfo(
            className: classSymbol.Name,
            methodName: methodSymbol.Name,
            fullToolName: fullToolName,
            methodSnakeName: methodSnakeName,
            description: description,
            parameters: parameters,
            returnType: methodSymbol.ReturnType,
            isAsync: isAsync,
            asyncInnerType: asyncInnerType,
            isValueTask: isValueTask,
            methodSymbol: methodSymbol);
    }

    /// <summary>
    /// 分析单个方法参数，提取 [ParameterAttribute] 信息并验证类型。
    /// </summary>
    private static ToolParamInfo? AnalyzeParameter(
        IParameterSymbol paramSymbol,
        string className,
        string methodName,
        Action<Diagnostic> reportDiagnostic)
    {
        // CNPG003: 不支持的参数类型
        var jsonType = TypeMapper.MapToJsonType(paramSymbol.Type);
        if (jsonType == null)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnsupportedParameterType,
                paramSymbol.Locations.FirstOrDefault(),
                $"{className}.{methodName}",
                paramSymbol.Name,
                paramSymbol.Type.ToDisplayString()));
            return null;
        }

        // 提取 [ParameterAttribute]
        var paramAttr = paramSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ParameterAttributeName);

        string paramName = paramSymbol.Name;
        string paramDescription = "";
        bool required = true;

        if (paramAttr != null)
        {
            var namedArgs = paramAttr.NamedArguments.ToDictionary(kv => kv.Key, kv => kv.Value);

            if (namedArgs.TryGetValue("Name", out var nameVal) && nameVal.Value is string n && !string.IsNullOrEmpty(n))
                paramName = n;

            if (namedArgs.TryGetValue("Description", out var descVal) && descVal.Value is string d)
                paramDescription = d;

            if (namedArgs.TryGetValue("Required", out var reqVal) && reqVal.Value is bool r)
                required = r;
        }

        // 可空引用类型默认非必填
        if (paramSymbol.NullableAnnotation == NullableAnnotation.Annotated)
            required = false;

        // 可空值类型（Nullable<T>）默认非必填
        if (paramSymbol.Type is INamedTypeSymbol namedType
            && namedType.IsGenericType
            && namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            required = false;
        }

        var jsonName = TypeMapper.ToSnakeCase(paramName);

        return new ToolParamInfo(
            paramName: paramName,
            jsonName: jsonName,
            description: paramDescription,
            required: required,
            jsonType: jsonType,
            typeSymbol: paramSymbol.Type,
            codeParamName: paramSymbol.Name);
    }
}
