using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Netor.Cortana.Plugin.Native.Generator.Diagnostics;

namespace Netor.Cortana.Plugin.Native.Generator.Analysis;

/// <summary>
/// 分析标记了 [Plugin] 的 static partial class Startup，提取插件元数据并验证约束。
/// </summary>
internal static class PluginClassAnalyzer
{
    private const string PluginAttributeName = "Netor.Cortana.Plugin.PluginAttribute";
    private const string ServiceCollectionInterfaceName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

    /// <summary>
    /// 从编译上下文中查找标记了 [Plugin] 的 static partial class，验证约束并提取元数据。
    /// </summary>
    public static PluginClassInfo? Analyze(
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic)
    {
        var pluginClasses = new List<(ClassDeclarationSyntax Syntax, INamedTypeSymbol Symbol)>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var classSyntax in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classSyntax);
                if (classSymbol == null)
                    continue;

                // 检查是否有 [Plugin] 标记
                var hasPluginAttr = classSymbol.GetAttributes()
                    .Any(a => a.AttributeClass?.ToDisplayString() == PluginAttributeName);

                if (!hasPluginAttr)
                    continue;

                pluginClasses.Add((classSyntax, classSymbol));
            }
        }

        if (pluginClasses.Count == 0)
            return null;

        // CNPG010: 多个插件入口类
        if (pluginClasses.Count > 1)
        {
            var names = string.Join(", ", pluginClasses.Select(p => p.Symbol.Name));
            foreach (var (syntax, _) in pluginClasses)
            {
                reportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MultiplePluginClasses,
                    syntax.Identifier.GetLocation(),
                    names));
            }
            return null;
        }

        var (pluginSyntax, pluginSymbol) = pluginClasses[0];

        // CNPG011: 必须是 static partial class
        bool isStatic = pluginSyntax.Modifiers.Any(SyntaxKind.StaticKeyword);
        bool isPartial = pluginSyntax.Modifiers.Any(SyntaxKind.PartialKeyword);

        if (!isStatic || !isPartial)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NotStaticPartialClass,
                pluginSyntax.Identifier.GetLocation(),
                pluginSymbol.Name));
            return null;
        }

        // CNPG005: 必须是 public
        if (pluginSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NotPublicClass,
                pluginSyntax.Identifier.GetLocation(),
                pluginSymbol.Name));
            return null;
        }

        // CNPG012: 必须包含 public static void Configure(IServiceCollection services) 方法
        var configureMethod = pluginSymbol.GetMembers("Configure")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m =>
                m.IsStatic
                && m.DeclaredAccessibility == Accessibility.Public
                && m.ReturnsVoid
                && m.Parameters.Length == 1
                && m.Parameters[0].Type.ToDisplayString() == ServiceCollectionInterfaceName);

        if (configureMethod == null)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MissingConfigureMethod,
                pluginSyntax.Identifier.GetLocation(),
                pluginSymbol.Name));
            return null;
        }

        // 提取 [Plugin] 属性
        var attrData = pluginSymbol.GetAttributes()
            .First(a => a.AttributeClass?.ToDisplayString() == PluginAttributeName);

        var namedArgs = attrData.NamedArguments.ToDictionary(kv => kv.Key, kv => kv.Value);

        var id = GetNamedArgString(namedArgs, "Id");
        var name = GetNamedArgString(namedArgs, "Name");
        var version = GetNamedArgString(namedArgs, "Version") ?? "1.0.0";
        var description = GetNamedArgString(namedArgs, "Description") ?? "";
        var instructions = GetNamedArgString(namedArgs, "Instructions");

        string[] tags = Array.Empty<string>();
        if (namedArgs.TryGetValue("Tags", out var tagsValue) && !tagsValue.IsNull)
        {
            tags = tagsValue.Values
                .Where(v => v.Value is string)
                .Select(v => (string)v.Value!)
                .ToArray();
        }

        // CNPG009: 缺少必填属性
        if (string.IsNullOrWhiteSpace(id))
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MissingRequiredAttribute,
                pluginSyntax.Identifier.GetLocation(),
                "Id"));
            return null;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MissingRequiredAttribute,
                pluginSyntax.Identifier.GetLocation(),
                "Name"));
            return null;
        }

        // CNPG019: Plugin Id 格式校验
        if (!IsValidPluginId(id!))
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidPluginId,
                pluginSyntax.Identifier.GetLocation(),
                id!));
            return null;
        }

        return new PluginClassInfo(pluginSymbol, id!, name!, version, description, tags, instructions);
    }

    /// <summary>
    /// 验证 Plugin Id 是否合法（仅允许小写字母、数字和下划线）。
    /// </summary>
    private static bool IsValidPluginId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        foreach (var c in id)
        {
            if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_')
                return false;
        }

        return true;
    }

    private static string? GetNamedArgString(Dictionary<string, TypedConstant> args, string key)
    {
        if (args.TryGetValue(key, out var value) && value.Value is string s)
            return s;
        return null;
    }
}
