using Microsoft.Extensions.Logging;

using System.Net.Http;

namespace Netor.Cortana.Plugin.Abstractions;

/// <summary>
/// 宿主向插件暴露的有限上下文，避免插件直接访问宿主内部。
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// 插件专属的数据存储目录。
    /// </summary>
    string DataDirectory { get; }

    /// <summary>
    /// 当前工作区目录。
    /// </summary>
    string WorkspaceDirectory { get; }

    /// <summary>
    /// 获取宿主提供的日志工厂。
    /// </summary>
    ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// 获取宿主提供的 HttpClientFactory。
    /// </summary>
    IHttpClientFactory HttpClientFactory { get; }
    /// <summary>
    /// 获取 WebSocket 服务器端口，供插件建立 WS 连接。
    /// </summary>
    int WsPort { get; }
}