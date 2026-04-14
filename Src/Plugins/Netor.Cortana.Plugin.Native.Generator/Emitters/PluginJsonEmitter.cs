using Netor.Cortana.Plugin.Native.Generator.Analysis;

using System.Text;

namespace Netor.Cortana.Plugin.Native.Generator.Emitters;

/// <summary>
/// 生成 plugin.json 清单文件的内容。
/// 通过 AdditionalFileOutput 输出到构建目录。
/// </summary>
internal static class PluginJsonEmitter
{
    /// <summary>
    /// 生成 plugin.json 内容。
    /// </summary>
    /// <param name="plugin">插件元数据。</param>
    /// <param name="assemblyName">项目的 AssemblyName（推断 libraryName）。</param>
    public static string Emit(PluginClassInfo plugin, string? assemblyName)
    {
        var libraryName = (assemblyName ?? "Plugin") + ".dll";

        var sb = new StringBuilder();
        sb.AppendLine("//{");
        sb.AppendLine($"//    \"id\": \"{EscapeJson(plugin.Id)}\",");
        sb.AppendLine($"//    \"name\": \"{EscapeJson(plugin.Name)}\",");
        sb.AppendLine($"//    \"version\": \"{EscapeJson(plugin.Version)}\",");
        sb.AppendLine($"//    \"description\": \"{EscapeJson(plugin.Description)}\",");
        sb.AppendLine("//    \"runtime\": \"native\",");
        sb.AppendLine($"//    \"libraryName\": \"{EscapeJson(libraryName)}\",");
        sb.AppendLine("//    \"minHostVersion\": \"1.0.0\"");
        sb.Append("//}");

        return sb.ToString();
    }

    private static string EscapeJson(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }
}