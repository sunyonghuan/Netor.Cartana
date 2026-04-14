using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Netor.Cortana.Plugin.Abstractions;

namespace Netor.Cortana.Plugin.Native;

/// <summary>
/// 原生通道的插件宿主。
/// 启动独立的 NativeHost 子进程来加载原生 DLL，通过 stdin/stdout JSON 协议通信。
/// 子进程崩溃不会影响宿主进程，实现完全的进程隔离。
/// </summary>
public sealed class NativePluginHost : IDisposable
{
    private readonly ILogger<NativePluginHost> _logger;
    private readonly List<IPlugin> _plugins = [];
    private Process? _hostProcess;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

    /// <summary>已加载的插件实例列表。</summary>
    public IReadOnlyList<IPlugin> Plugins => _plugins;

    /// <summary>插件目录路径。</summary>
    public string PluginDirectory { get; }

    /// <summary>插件清单。</summary>
    public PluginManifest Manifest { get; }

    /// <summary>子进程是否正在运行。</summary>
    public bool IsProcessAlive => _hostProcess is { HasExited: false };

    public NativePluginHost(string pluginDirectory, PluginManifest manifest, ILogger<NativePluginHost> logger)
    {
        PluginDirectory = pluginDirectory;
        Manifest = manifest;
        _logger = logger;
    }

    /// <summary>
    /// 启动 NativeHost 子进程，加载原生 DLL 并初始化插件。
    /// </summary>
    public async Task LoadAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var libraryPath = Path.Combine(PluginDirectory, Manifest.LibraryName!);

        if (!File.Exists(libraryPath))
        {
            _logger.LogWarning("原生 DLL 不存在：{Path}", libraryPath);
            return;
        }

        try
        {
            StartHostProcess(libraryPath);
            await InitializePluginAsync(context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "加载原生插件失败：{Dir}", PluginDirectory);
            KillProcess();
        }
    }

    /// <summary>
    /// 向子进程发送请求并等待响应。
    /// 如果子进程已崩溃，返回错误响应。
    /// </summary>
    public async Task<NativeHostResponse> SendRequestAsync(NativeHostRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsProcessAlive)
        {
            return new NativeHostResponse
            {
                Success = false,
                Error = "原生插件宿主进程已退出"
            };
        }

        await _sendLock.WaitAsync(cancellationToken);

        try
        {
            var json = JsonSerializer.Serialize(request, NativePluginJsonContext.Default.NativeHostRequest);
            await _writer!.WriteLineAsync(json.AsMemory(), cancellationToken);
            await _writer.FlushAsync(cancellationToken);

            var responseLine = await ReadLineWithTimeoutAsync(TimeSpan.FromSeconds(30), cancellationToken);

            if (responseLine is null)
            {
                return new NativeHostResponse
                {
                    Success = false,
                    Error = "原生插件宿主进程无响应或已退出"
                };
            }

            var response = JsonSerializer.Deserialize(responseLine, NativePluginJsonContext.Default.NativeHostResponse);
            return response ?? new NativeHostResponse { Success = false, Error = "响应反序列化失败" };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "与原生插件宿主进程通信失败");
            return new NativeHostResponse { Success = false, Error = ex.Message };
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 尝试优雅关闭：发送 destroy 命令
        if (IsProcessAlive)
        {
            try
            {
                var request = new NativeHostRequest { Method = NativeHostMethods.Destroy };
                var json = JsonSerializer.Serialize(request, NativePluginJsonContext.Default.NativeHostRequest);
                _writer?.WriteLine(json);
                _writer?.Flush();

                // 给子进程 3 秒时间清理
                _hostProcess?.WaitForExit(3000);
            }
            catch
            {
                // 清理阶段忽略通信异常
            }
        }

        KillProcess();
        _sendLock.Dispose();
        _plugins.Clear();
    }

    // ──────── 私有方法 ────────

    /// <summary>
    /// 启动 NativeHost 子进程。
    /// </summary>
    private void StartHostProcess(string libraryPath)
    {
        var hostExePath = GetHostExePath();

        if (!File.Exists(hostExePath))
        {
            throw new FileNotFoundException(
                $"NativeHost 可执行文件不存在：{hostExePath}。请确保已编译 Netor.Cortana.NativeHost 项目。");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = hostExePath,
            Arguments = $"\"{libraryPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = PluginDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        _hostProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("启动 NativeHost 子进程失败");

        _writer = _hostProcess.StandardInput;
        _writer.AutoFlush = false;
        _reader = _hostProcess.StandardOutput;

        // 子进程的 stderr 输出转到日志
        _hostProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _logger.LogWarning("[NativeHost:{Id}] {Stderr}", Manifest.Id, e.Data);
        };
        _hostProcess.BeginErrorReadLine();

        // 监听进程退出
        _hostProcess.EnableRaisingEvents = true;
        _hostProcess.Exited += (_, _) =>
        {
            _logger.LogWarning(
                "NativeHost 子进程已退出（插件 {Id}，退出码 {Code}）",
                Manifest.Id, _hostProcess?.ExitCode);
        };

        _logger.LogDebug("NativeHost 子进程已启动（PID={Pid}，库={Lib}）", _hostProcess.Id, libraryPath);
    }

    /// <summary>
    /// 通过协议初始化插件：get_info → init → 构建 wrapper。
    /// </summary>
    private async Task InitializePluginAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        // 1. 获取插件信息
        var infoResponse = await SendRequestAsync(
            new NativeHostRequest { Method = NativeHostMethods.GetInfo },
            cancellationToken);

        if (!infoResponse.Success || string.IsNullOrWhiteSpace(infoResponse.Data))
        {
            _logger.LogWarning("获取原生插件信息失败：{Error}", infoResponse.Error ?? "空数据");
            KillProcess();
            return;
        }

        NativePluginInfo? info;

        try
        {
            info = JsonSerializer.Deserialize(infoResponse.Data, NativePluginJsonContext.Default.NativePluginInfo);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "原生插件信息 JSON 解析失败");
            KillProcess();
            return;
        }

        if (info is null || string.IsNullOrWhiteSpace(info.Id))
        {
            _logger.LogWarning("原生插件信息无效（缺少 id）");
            KillProcess();
            return;
        }

        // 2. 初始化插件
        var configJson = JsonSerializer.Serialize(new NativePluginInitConfig
        {
            DataDirectory = Path.Combine(PluginDirectory, "data"),
            WorkspaceDirectory = context.WorkspaceDirectory, 
            WsPort = context.WsPort,
            PluginDirectory = PluginDirectory
        }, NativePluginJsonContext.Default.NativePluginInitConfig);

        var initResponse = await SendRequestAsync(
            new NativeHostRequest { Method = NativeHostMethods.Init, Args = configJson },
            cancellationToken);

        if (!initResponse.Success)
        {
            _logger.LogWarning("原生插件 [{Id}] 初始化失败：{Error}", info.Id, initResponse.Error);
            KillProcess();
            return;
        }

        // 3. 创建 IPlugin 包装器
        var wrapper = new NativePluginWrapper(this, info);
        _plugins.Add(wrapper);

        _logger.LogInformation(
            "已加载原生插件：{Id} ({Name} v{Version})，工具数：{ToolCount}",
            info.Id, info.Name, info.Version, wrapper.Tools.Count);
    }

    /// <summary>
    /// 带超时的读取一行。
    /// </summary>
    private async Task<string?> ReadLineWithTimeoutAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (_reader is null) return null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await _reader.ReadLineAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("原生插件宿主进程响应超时（{Timeout}s）", timeout.TotalSeconds);
            return null;
        }
    }

    /// <summary>
    /// 强制终止子进程。
    /// </summary>
    private void KillProcess()
    {
        try
        {
            if (_hostProcess is { HasExited: false })
            {
                _hostProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 忽略终止异常
        }
        finally
        {
            _hostProcess?.Dispose();
            _hostProcess = null;
            _writer = null;
            _reader = null;
        }
    }

    /// <summary>
    /// 获取 NativeHost 可执行文件路径。
    /// 使用 Environment.ProcessPath 获取宿主 exe 的实际物理路径，
    /// 确保单文件发布（PublishSingleFile）模式下也能正确定位
    /// （AppContext.BaseDirectory 在单文件模式下指向临时解压目录）。
    /// </summary>
    private string GetHostExePath()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        var exeName = OperatingSystem.IsWindows()
            ? "Cortana.NativeHost.exe"
            : "Cortana.NativeHost";
        var path = Path.Combine(exeDir, exeName);

        if (!File.Exists(path))
        {
            _logger.LogError(
                "NativeHost 可执行文件未找到：{Path}（ExeDir={ExeDir}）", path, exeDir);
        }

        return path;
    }
}
