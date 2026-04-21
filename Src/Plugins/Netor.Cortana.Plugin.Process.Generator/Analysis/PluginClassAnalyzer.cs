using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Netor.Cortana.Plugin.Process.Generator.Diagnostics;

namespace Netor.Cortana.Plugin.Process.Generator.Analysis;

/// <summary>
/// 分析标记了 [Plugin] 的 partial class，提取插件元数据并验证约束。
/// <para>
/// Process 与 Native 的差异：
/// - Process 的入口类允许是非 static partial（通常就是 Program）；
/// - Configure 方法为可选（用户不需要注入额外依赖时可省略）。
/// </para>
/// </summary>
internal static class PluginClassAnalyzer
{
    private const string PluginAttributeName = "Netor.Cortana.Plugin.PluginAttribute";
    private const string ServiceCollectionInterfaceName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

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
                if (classSymbol == null) continue;

                var hasPluginAttr = classSymbol.GetAttributes()
                    .Any(a => a.AttributeClass?.ToDisplayString() == PluginAttributeName);

                if (!hasPluginAttr) continue;

                pluginClasses.Add((classSyntax, classSymbol));
            }
        }

        if (pluginClasses.Count == 0)
            return null;

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

        // CPPG011: 必须是 partial（允许非 static）
        bool isPartial = pluginSyntax.Modifiers.Any(SyntaxKind.PartialKeyword);
        if (!isPartial)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NotPartialClass,
                pluginSyntax.Identifier.GetLocation(),
                pluginSymbol.Name));
            return null;
        }

        // CPPG005: 必须是 public 或 internal（允许 Program 默认的 internal）
        if (pluginSymbol.DeclaredAccessibility != Accessibility.Public
            && pluginSymbol.DeclaredAccessibility != Accessibility.Internal
            && pluginSymbol.DeclaredAccessibility != Accessibility.NotApplicable)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NotPublicClass,
                pluginSyntax.Identifier.GetLocation(),
                pluginSymbol.Name));
            return null;
        }

        // Configure 方法可选：检查是否存在 public/internal static void Configure(IServiceCollection)
        var hasConfigure = pluginSymbol.GetMembers("Configure")
            .OfType<IMethodSymbol>()
            .Any(m =>
                m.IsStatic
                && m.ReturnsVoid
                && m.Parameters.Length == 1
                && m.Parameters[0].Type.ToDisplayString() == ServiceCollectionInterfaceName);

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

        if (!IsValidPluginId(id!))
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidPluginId,
                pluginSyntax.Identifier.GetLocation(),
                id!));
            return null;
        }

        return new PluginClassInfo(
            pluginSymbol, id!, name!, version, description, tags, instructions, hasConfigure);
    }

    private static bool IsValidPluginId(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
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
