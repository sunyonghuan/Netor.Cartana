using System.Text.Json;

namespace Netor.Cortana.Plugin;

/// <summary>
/// 插件运行时配置，由宿主在 init 阶段注入。
/// <para>
/// Native 通道：通过 <c>cortana_plugin_init</c> 的 config JSON 注入。<br/>
/// Process 通道：通过 stdin 发送的 <c>init</c> 消息注入。
/// </para>
/// 两种通道下插件均可在工具类构造函数中直接注入本类型。
/// </summary>
public sealed class PluginSettings
{
    /// <summary>
    /// 插件专属的数据存储目录。插件应将持久化数据写入此目录。
    /// </summary>
    public string DataDirectory { get; }

    /// <summary>
    /// 当前工作区目录。
    /// </summary>
    public string WorkspaceDirectory { get; }

    /// <summary>
    /// 插件目录，位于 <c>plugins/</c> 下，包含插件文件和资源。
    /// 可用于访问插件内置的资源文件。
    /// </summary>
    public string PluginDirectory { get; }

    /// <summary>
    /// WebSocket 服务器端口，供插件建立 WS 连接（0 表示不可用）。
    /// </summary>
    public int WsPort { get; }

    /// <summary>
    /// 直接构造（供 Debugger 和内部使用）。
    /// </summary>
    public PluginSettings(
        string dataDirectory,
        string workspaceDirectory,
        string pluginDirectory,
        int wsPort)
    {
        DataDirectory = dataDirectory;
        WorkspaceDirectory = workspaceDirectory;
        PluginDirectory = pluginDirectory;
        WsPort = wsPort;
    }

    /// <summary>
    /// 从宿主传入的 config JSON 解析配置。
    /// </summary>
    /// <param name="json">
    /// 格式：<c>{"dataDirectory":"...","workspaceDirectory":"...","pluginDirectory":"...","wsPort":12345}</c>
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

        return new PluginSettings(dataDirectory, workspaceDirectory, pluginDirectory, wsPort);
    }
}
