using Netor.Cortana.Plugin.Native.Generator.Analysis;

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Netor.Cortana.Plugin.Native.Generator.Emitters;

/// <summary>
/// 生成 cortana_plugin_get_info 导出函数中的 JSON 常量字符串。
/// </summary>
internal static class InfoJsonEmitter
{
    /// <summary>
    /// 生成 get_info 返回的 JSON 常量。
    /// </summary>
    public static string EmitInfoJson(PluginClassInfo plugin, List<ToolClassInfo> toolClasses)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"    \"id\": \"{EscapeJson(plugin.Id)}\",");
        sb.AppendLine($"    \"name\": \"{EscapeJson(plugin.Name)}\",");
        sb.AppendLine($"    \"version\": \"{EscapeJson(plugin.Version)}\",");
        sb.AppendLine($"    \"description\": \"{EscapeJson(plugin.Description)}\",");

        if (plugin.Instructions != null)
        {
            sb.AppendLine($"    \"instructions\": \"{EscapeJson(plugin.Instructions)}\",");
        }

        // Tags
        sb.Append("    \"tags\": [");
        if (plugin.Tags.Length > 0)
        {
            sb.Append(string.Join(", ", plugin.Tags.Select(t => $"\"{EscapeJson(t)}\"")));
        }
        sb.AppendLine("],");

        // Tools
        sb.AppendLine("    \"tools\": [");

        var allMethods = toolClasses.SelectMany(tc => tc.Methods).ToList();

        for (int i = 0; i < allMethods.Count; i++)
        {
            var method = allMethods[i];
            sb.AppendLine("        {");
            sb.AppendLine($"            \"name\": \"{EscapeJson(method.FullToolName)}\",");
            sb.AppendLine($"            \"description\": \"{EscapeJson(method.Description)}\",");
            sb.Append("            \"parameters\": [");

            if (method.Parameters.Count > 0)
            {
                sb.AppendLine();
                for (int j = 0; j < method.Parameters.Count; j++)
                {
                    var param = method.Parameters[j];
                    sb.Append($"                {{ \"name\": \"{EscapeJson(param.JsonName)}\", \"type\": \"{param.JsonType}\", \"description\": \"{EscapeJson(param.Description)}\", \"required\": {(param.Required ? "true" : "false")} }}");
                    if (j < method.Parameters.Count - 1)
                        sb.AppendLine(",");
                    else
                        sb.AppendLine();
                }
                sb.Append("            ");
            }

            sb.AppendLine("]");

            sb.Append("        }");
            if (i < allMethods.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.AppendLine("    ]");
        sb.Append("}");

        return sb.ToString();
    }

    private static string EscapeJson(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}