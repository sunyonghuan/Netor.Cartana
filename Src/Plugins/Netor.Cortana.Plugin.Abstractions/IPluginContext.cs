using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Netor.Cortana.Plugin;

/// <summary>
/// 插件运行时上下文，宿主为每个插件实例提供的环境访问接口。
/// 插件通过此接口访问日志、HTTP 客户端、数据目录等宿主服务。
/// </summary>
public interface IPluginContext
{
    /// <summary>插件的专用数据目录（宿主保证已创建）。</summary>
    string DataDirectory { get; }

    /// <summary>工作空间目录（通常是 Cortana 的配置/缓存目录）。</summary>
    string WorkspaceDirectory { get; }

    /// <summary>日志工厂（用于插件内部的日志记录）。</summary>
    ILoggerFactory LoggerFactory { get; }

    /// <summary>HTTP 客户端工厂（用于插件发起 HTTP 请求）。</summary>
    IHttpClientFactory HttpClientFactory { get; }

    /// <summary>WebSocket 服务端口号（如果宿主启用 WebSocket 服务）。</summary>
    int WsPort { get; }
}
