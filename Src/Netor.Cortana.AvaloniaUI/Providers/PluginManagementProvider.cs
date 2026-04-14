using System.Text;

using Netor.Cortana.Plugin;

namespace Netor.Cortana.AvaloniaUI.Providers;

/// <summary>
/// 插件管理工具提供者，向 AI 提供已加载插件的查询、卸载与重载能力，用于插件热更新。
/// </summary>
internal sealed class PluginManagementProvider(
    ILogger<PluginManagementProvider> logger,
    PluginLoader pluginLoader) : AIContextProvider
{
    private readonly List<AITool> _tools = [];

    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        if (_tools.Count == 0)
            RegisterTools();

        return ValueTask.FromResult(new AIContext
        {
            Instructions = BuildInstructions(),
            Tools = _tools
        });
    }

    private void RegisterTools()
    {
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_list_loaded_plugins",
            description: "列出当前已加载的所有插件（含原生插件和 MCP 服务），返回带序号的列表。包含插件目录名（用于卸载/重载）。",
            method: ListLoadedPlugins));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_unload_plugin",
            description: "卸载指定插件，释放其对文件的占用。参数：dirName（插件目录名，通过 sys_list_loaded_plugins 获取）。卸载后可替换插件文件再重载。",
            method: (string dirName) => UnloadPlugin(dirName)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_reload_plugin",
            description: "重新加载指定插件（先卸载再加载）。参数：dirName（插件目录名，通过 sys_list_loaded_plugins 获取）。用于插件文件替换后重新加载。",
            method: (string dirName) => ReloadPluginAsync(dirName)));
    }

    private static string BuildInstructions() =>
        """
        你可以管理已加载的插件：查看列表、卸载、重载。
        更新插件的流程：先调用 sys_unload_plugin 卸载目标插件 → 替换插件文件 → 再调用 sys_reload_plugin 重新加载。
        卸载插件会终止其子进程并释放文件占用，之后才能替换文件。
        """;

    private string ListLoadedPlugins()
    {
        var sb = new StringBuilder();

        // 原生插件（Native）
        var nativePlugins = pluginLoader.GetActivePlugins();
        if (nativePlugins.Count > 0)
        {
            sb.AppendLine("原生插件：");
            for (int i = 0; i < nativePlugins.Count; i++)
            {
                var p = nativePlugins[i];
                sb.AppendLine($"  {i + 1}. {p.Name} (v{p.Version}) [id={p.Id}]");
            }
        }

        // MCP 服务
        var mcpServers = pluginLoader.GetActiveMcpServers();
        if (mcpServers.Count > 0)
        {
            sb.AppendLine("MCP 服务：");
            for (int i = 0; i < mcpServers.Count; i++)
            {
                var m = mcpServers[i];
                sb.AppendLine($"  {i + 1}. {m.Name} [id={m.Id}]");
            }
        }

        // 插件目录
        var dirNames = pluginLoader.GetLoadedPluginDirNames();
        if (dirNames.Count > 0)
        {
            sb.AppendLine("插件目录（用于卸载/重载）：");
            foreach (var dir in dirNames)
                sb.AppendLine($"  - {dir}");
        }

        if (sb.Length == 0)
            return "当前没有已加载的插件。";

        return sb.ToString();
    }

    private string UnloadPlugin(string dirName)
    {
        if (string.IsNullOrWhiteSpace(dirName))
            return "✗ 插件目录名不能为空。";

        try
        {
            pluginLoader.UnloadPlugin(dirName);
            logger.LogInformation("已卸载插件：{DirName}", dirName);
            return $"✓ 已卸载插件「{dirName}」，文件占用已释放，现在可以替换文件。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "卸载插件失败：{DirName}", dirName);
            return $"✗ 卸载插件失败：{ex.Message}";
        }
    }

    private async Task<string> ReloadPluginAsync(string dirName)
    {
        if (string.IsNullOrWhiteSpace(dirName))
            return "✗ 插件目录名不能为空。";

        try
        {
            await pluginLoader.ReloadPluginAsync(dirName);
            logger.LogInformation("已重载插件：{DirName}", dirName);
            return $"✓ 已重载插件「{dirName}」。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "重载插件失败：{DirName}", dirName);
            return $"✗ 重载插件失败：{ex.Message}";
        }
    }
}
