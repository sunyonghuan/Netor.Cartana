using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using ModelContextProtocol.Client;

using Netor.Cortana.Entitys;

namespace Netor.Cortana.Plugin.Mcp;

/// <summary>
/// 单个 MCP Server 的生命周期管理器。
/// 根据 <see cref="McpServerEntity"/> 的配置创建对应的传输层并建立连接，
/// 获取工具列表后供 <see cref="McpContextProvider"/> 使用。
/// </summary>
public sealed class McpServerHost : IAsyncDisposable
{
    private readonly McpServerEntity _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<McpServerHost> _logger;

    private McpClient? _client;
    private IList<McpClientTool>? _tools;

    /// <summary>
    /// MCP 服务器的数据库 ID。
    /// </summary>
    public string Id => _config.Id;

    /// <summary>
    /// MCP 服务器的显示名称。
    /// </summary>
    public string Name => _config.Name;

    /// <summary>
    /// MCP 服务器的描述信息。
    /// </summary>
    public string Description => _config.Description;

    /// <summary>
    /// 当前连接获取到的工具列表。
    /// </summary>
    public IReadOnlyList<AITool> Tools => (IReadOnlyList<AITool>?)_tools ?? [];

    /// <summary>
    /// 连接是否已建立。
    /// </summary>
    public bool IsConnected => _client is not null;

    public McpServerHost(McpServerEntity config, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<McpServerHost>();
    }

    /// <summary>
    /// 根据配置创建传输层并连接到 MCP Server，获取工具列表。
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IClientTransport transport = _config.TransportType.ToLowerInvariant() switch
            {
                "stdio" => CreateStdioTransport(),
                "sse" or "streamable-http" => CreateHttpTransport(),
                _ => throw new InvalidOperationException($"不支持的传输类型：{_config.TransportType}")
            };

            _client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions { ClientInfo = new() { Name = "Netor.Cortana", Version = "1.0.0", Title = "Netor.Cortana" } },
                _loggerFactory,
                cancellationToken);

            _tools = await _client.ListToolsAsync(cancellationToken: cancellationToken);

            _logger.LogInformation(
                "MCP Server [{Name}] 连接成功，获取到 {Count} 个工具",
                _config.Name, _tools.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Server [{Name}] 连接失败", _config.Name);
            throw;
        }
    }

    /// <summary>
    /// 重新获取工具列表（用于工具变更通知后刷新）。
    /// </summary>
    public async Task RefreshToolsAsync(CancellationToken cancellationToken = default)
    {
        if (_client is null) return;

        _tools = await _client.ListToolsAsync(cancellationToken: cancellationToken);

        _logger.LogInformation(
            "MCP Server [{Name}] 工具列表已刷新，共 {Count} 个工具",
            _config.Name, _tools.Count);
    }

    private StdioClientTransport CreateStdioTransport()
    {
        if (string.IsNullOrWhiteSpace(_config.Command))
            throw new InvalidOperationException($"MCP Server [{_config.Name}] 的启动命令未配置");

        var options = new StdioClientTransportOptions
        {
            Command = _config.Command,
            Arguments = _config.Arguments,
            Name = _config.Name
        };

        // 合并环境变量
        if (_config.EnvironmentVariables.Count > 0)
        {
            options.EnvironmentVariables = new Dictionary<string, string?>(_config.EnvironmentVariables);
        }

        return new StdioClientTransport(options, _loggerFactory);
    }

    private HttpClientTransport CreateHttpTransport()
    {
        if (string.IsNullOrWhiteSpace(_config.Url))
            throw new InvalidOperationException($"MCP Server [{_config.Name}] 的 HTTP 地址未配置");

        var options = new HttpClientTransportOptions
        {
            Endpoint = new Uri(_config.Url),
            Name = _config.Name,
            TransportMode = string.Equals(_config.TransportType, "sse", StringComparison.OrdinalIgnoreCase)
                ? HttpTransportMode.Sse
                : HttpTransportMode.StreamableHttp
        };

        // 添加认证头
        if (!string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            options.AdditionalHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {_config.ApiKey}"
            };
        }

        return new HttpClientTransport(options, _loggerFactory);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            _logger.LogInformation("正在断开 MCP Server [{Name}]", _config.Name);

            await _client.DisposeAsync();
            _client = null;
            _tools = null;
        }
    }
}
