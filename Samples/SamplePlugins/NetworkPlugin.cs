using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Netor.Cortana.Plugin.Abstractions;

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace SamplePlugins;

/// <summary>
/// 网络工具插件 — 提供 Ping、DNS 查询、端口检测等网络诊断工具。
/// </summary>
public sealed class NetworkPlugin : IPlugin, IDisposable
{
    private readonly List<AITool> _tools = [];
    private ILogger? _logger;
    private HttpClient? _http;

    public string Id => "com.sample.network";
    public string Name => "网络工具";
    public Version Version => new(1, 0, 0);
    public string Description => "提供 Ping、DNS 查询、端口检测、URL 编解码等网络诊断能力";
    public IReadOnlyList<string> Tags => ["网络", "工具"];
    public IReadOnlyList<AITool> Tools => _tools;

    public string? Instructions => """
        当用户需要网络诊断或编码转换时，使用以下工具：
        - sys_net_ping: Ping 指定主机
        - sys_net_dns_lookup: DNS 查询（域名解析为 IP）
        - sys_net_http_head: 对 URL 执行 HTTP HEAD 请求
        - sys_net_port_check: 检测指定主机的端口是否开放
        - sys_net_ip_info: 查询 IP 地址的地理位置信息
        - sys_net_url_encode: URL 编码/解码
        - sys_net_base64: Base64 编码/解码
        """;

    public Task InitializeAsync(IPluginContext context)
    {
        _logger = context.LoggerFactory.CreateLogger<NetworkPlugin>();
        _http = context.HttpClientFactory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(10);

        _tools.Add(AIFunctionFactory.Create(PingHost, "sys_net_ping", "Ping 指定主机并返回延迟信息"));
        _tools.Add(AIFunctionFactory.Create(DnsLookup, "sys_net_dns_lookup", "DNS 查询，将域名解析为 IP 地址"));
        _tools.Add(AIFunctionFactory.Create(HttpHead, "sys_net_http_head", "对 URL 执行 HTTP HEAD 请求，返回响应头信息"));
        _tools.Add(AIFunctionFactory.Create(PortCheck, "sys_net_port_check", "检测指定主机的 TCP 端口是否开放"));
        _tools.Add(AIFunctionFactory.Create(IpInfo, "sys_net_ip_info", "查询 IP 地址的地理位置信息"));
        _tools.Add(AIFunctionFactory.Create(UrlEncodeDecode, "sys_net_url_encode", "URL 编码或解码"));
        _tools.Add(AIFunctionFactory.Create(Base64EncodeDecode, "sys_net_base64", "Base64 编码或解码"));

        _logger.LogInformation("NetworkPlugin 初始化完成，注册 {Count} 个工具", _tools.Count);
        return Task.CompletedTask;
    }

    private async Task<string> PingHost(string host, int count = 4)
    {
        count = Math.Clamp(count, 1, 10);
        _logger?.LogDebug("Ping {Host} x{Count}", host, count);

        try
        {
            using var ping = new Ping();
            var results = new List<string>();
            long totalMs = 0;
            int success = 0;

            for (int i = 0; i < count; i++)
            {
                var reply = await ping.SendPingAsync(host, 3000);
                if (reply.Status == IPStatus.Success)
                {
                    results.Add($"  来自 {reply.Address}: 字节=32 时间={reply.RoundtripTime}ms TTL={reply.Options?.Ttl}");
                    totalMs += reply.RoundtripTime;
                    success++;
                }
                else
                {
                    results.Add($"  请求超时（{reply.Status}）");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"正在 Ping {host}：");
            results.ForEach(r => sb.AppendLine(r));
            sb.AppendLine($"统计: 发送={count}, 接收={success}, 丢失={count - success} ({(count - success) * 100 / count}% 丢失)");
            if (success > 0)
                sb.AppendLine($"平均延迟: {totalMs / success}ms");

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"Ping {host} 失败：{ex.Message}";
        }
    }

    private async Task<string> DnsLookup(string hostname)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(hostname);
            var ips = entry.AddressList.Select(ip =>
                $"  {ip} ({(ip.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6")})");
            return $"DNS 解析 {hostname}：\n{string.Join("\n", ips)}";
        }
        catch (Exception ex)
        {
            return $"DNS 解析 {hostname} 失败：{ex.Message}";
        }
    }

    private async Task<string> HttpHead(string url)
    {
        if (_http is null) return "错误：HTTP 客户端未初始化";

        try
        {
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _http.SendAsync(request);

            var sb = new StringBuilder();
            sb.AppendLine($"HTTP HEAD {url}");
            sb.AppendLine($"状态: {(int)response.StatusCode} {response.ReasonPhrase}");

            foreach (var header in response.Headers.Concat(response.Content.Headers))
                sb.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"HTTP HEAD {url} 失败：{ex.Message}";
        }
    }

    private async Task<string> PortCheck(string host, int port, int timeoutMs = 3000)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeoutMs));

            if (completed == connectTask && client.Connected)
                return $"✅ {host}:{port} 端口开放";
            else
                return $"❌ {host}:{port} 端口关闭或超时";
        }
        catch (Exception ex)
        {
            return $"❌ {host}:{port} 连接失败：{ex.Message}";
        }
    }

    private async Task<string> IpInfo(string ip)
    {
        if (_http is null) return "错误：HTTP 客户端未初始化";

        try
        {
            // 使用免费的 ip-api.com
            var json = await _http.GetStringAsync($"http://ip-api.com/json/{ip}?lang=zh-CN");
            return $"IP 信息查询 {ip}：\n{json}";
        }
        catch (Exception ex)
        {
            return $"IP 信息查询失败：{ex.Message}";
        }
    }

    private string UrlEncodeDecode(string text, string action = "encode")
    {
        return action.ToLowerInvariant() switch
        {
            "encode" => $"编码结果：{Uri.EscapeDataString(text)}",
            "decode" => $"解码结果：{Uri.UnescapeDataString(text)}",
            _ => "请指定 action 为 encode 或 decode"
        };
    }

    private string Base64EncodeDecode(string text, string action = "encode")
    {
        try
        {
            return action.ToLowerInvariant() switch
            {
                "encode" => $"Base64 编码：{Convert.ToBase64String(Encoding.UTF8.GetBytes(text))}",
                "decode" => $"Base64 解码：{Encoding.UTF8.GetString(Convert.FromBase64String(text))}",
                _ => "请指定 action 为 encode 或 decode"
            };
        }
        catch (Exception ex)
        {
            return $"Base64 操作失败：{ex.Message}";
        }
    }

    public void Dispose() => _http?.Dispose();
}
