using System.Text.Json;

namespace Netor.Cortana.Plugin.Native;

/// <summary>
/// 插件运行时配置，由宿主通过 <c>cortana_plugin_init</c> 的 config JSON 注入。
/// <para>
/// 对应 <c>IPluginContext</c> 中的 DataDirectory/WorkspaceDirectory/WsPort。
/// 可在工具类构造函数中直接注入。
/// </para>
/// </summary>
public sealed class PluginSettings
{
    /// <summary>
    /// 插件专属的数据存储目录。
    /// </summary>
    public string DataDirectory { get; }

    /// <summary>
    /// 当前工作区目录。
    /// </summary>
    public string WorkspaceDirectory { get; }

    /// <summary>
    /// WebSocket 服务器端口，供插件建立 WS 连接。
    /// </summary>
    public int WsPort { get; }
    /// <summary>
    /// 插件目录，包含插件文件和资源，位于 <c>plugins/</c> 下，由宿主在加载插件时设置。可用于访问插件资源文件。
    /// </summary>
    public string PluginDirectory { get;}

    private PluginSettings(string dataDirectory, string workspaceDirectory,string pluginDirectory, int wsPort)
    {
        DataDirectory = dataDirectory;
        WorkspaceDirectory = workspaceDirectory;
        WsPort = wsPort;
        PluginDirectory= pluginDirectory;
    }

    /// <summary>
    /// 从宿主传入的 config JSON 解析配置。
    /// </summary>
    /// <param name="json">
    /// 格式：<c>{"dataDirectory":"...","workspaceDirectory":"...","wsPort":12345}</c>
    /// </param>
    public static PluginSettings FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var dataDirectory = root.TryGetProperty("dataDirectory", out var dd)
            ? dd.GetString() ?? string.Empty
            : string.Empty;

        var workspaceDirectory = root.TryGetProperty("workspaceDirectory", out var wd)
            ? wd.GetString() ?? string.Empty
            : string.Empty;
        var pluginDirectory = root.TryGetProperty("pluginDirectory", out var pd)
            ? pd.GetString() ?? string.Empty
            : string.Empty;

        int wsPort = root.TryGetProperty("wsPort", out var wp)
            ? wp.GetInt32()
            : 0;

        return new PluginSettings(dataDirectory, workspaceDirectory,pluginDirectory, wsPort);
    }
}
