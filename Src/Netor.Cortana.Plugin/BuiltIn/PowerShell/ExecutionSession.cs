using Microsoft.Extensions.Logging;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Netor.Cortana.Plugin.BuiltIn.PowerShell;

/// <summary>
/// 执行会话 - 持续交互的 PowerShell 或 SSH 会话
/// </summary>
public sealed class ExecutionSession : IAsyncDisposable
{
    private readonly ILogger<SessionRegistry> _logger;
    private readonly Process? _process;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    public string Id { get; }
    public string Type { get; }  // "local" 或 "remote"
    public string? Host { get; }
    public string? Username { get; }
    public DateTime CreatedAt { get; }
    public DateTime LastActivityAt { get; set; }
    public bool IsActive => !_disposed && _process is { HasExited: false };

    public ExecutionSession(string type, string? host, string? username, string? password, string? privateKeyPath, ILogger<SessionRegistry> logger)
    {
        Id = Guid.NewGuid().ToString("N");
        Type = type;
        Host = host;
        Username = username;
        CreatedAt = DateTime.Now;
        LastActivityAt = DateTime.Now;
        _logger = logger;

        if (type == "local")
        {
            _process = CreateLocalSession();
        }
        else if (type == "remote" && !string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(username))
        {
            _process = CreateRemoteSession(host, username, password, privateKeyPath);
        }

        // 验证进程是否启动成功
        if (_process is null || _process.HasExited)
        {
            _logger.LogError("会话进程启动失败或已立即退出: {SessionId}", Id);
            _disposed = true;
        }
        else
        {
            // 启动后台 stderr 排空任务，防止 stderr 缓冲区满导致死锁
            _ = DrainStderrAsync(_process);
        }
    }

    /// <summary>
    /// 创建本地 PowerShell 会话
    /// </summary>
    private Process CreateLocalSession()
    {
        var psi = new ProcessStartInfo
        {
            FileName = PowerShellPathHelper.GetPath(),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false,  // 用户可见
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        var process = new Process { StartInfo = psi };
        process.Start();
        _logger.LogInformation("本地 PowerShell 会话已启动: {SessionId}", Id);
        return process;
    }

    /// <summary>
    /// 创建远程 SSH 会话，优先使用密钥认证，无密钥时弹出窗口由用户输入密码
    /// </summary>
    private Process CreateRemoteSession(string host, string username, string? password, string? privateKeyPath)
    {
        var args = "-o StrictHostKeyChecking=no";

        if (!string.IsNullOrWhiteSpace(privateKeyPath))
        {
            // 密钥认证：-i 指定密钥文件，不使用 BatchMode=yes 以允许 passphrase 交互提示
            args += $" -i \"{privateKeyPath}\"";
            _logger.LogInformation("使用密钥认证: {KeyPath}", privateKeyPath);
        }

        args += $" {username}@{host}";

        var psi = new ProcessStartInfo
        {
            FileName = "ssh",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        var process = new Process { StartInfo = psi };
        process.Start();

        _logger.LogInformation("远程 SSH 会话已启动: {SessionId} - {Host} (认证方式: {AuthType})",
            Id, host, string.IsNullOrWhiteSpace(privateKeyPath) ? "密码" : "密钥");
        return process;
    }

    /// <summary>
    /// 后台排空 stderr，防止缓冲区满导致进程死锁
    /// </summary>
    private async Task DrainStderrAsync(Process process)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync() is { } line)
            {
                _logger.LogWarning("[Session {SessionId} stderr] {Line}", Id, line);
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            // 进程已关闭，正常退出
        }
    }

    /// <summary>
    /// 向会话发送命令并异步返回输出
    /// </summary>
    public async IAsyncEnumerable<string> ExecuteCommandAsync(
        string command,
        int timeoutMs = 30000,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!IsActive)
            throw new InvalidOperationException("会话已关闭");

        LastActivityAt = DateTime.Now;

        await _writeLock.WaitAsync(ct);
        try
        {
            // 发送命令
            await _process!.StandardInput.WriteLineAsync($"{command}; echo '___COMMAND_END___'");
            await _process.StandardInput.FlushAsync();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            // 读取输出直到命令标记
            while (!cts.Token.IsCancellationRequested)
            {
                // 使用 Task.WhenAny + 延迟实现可取消的 ReadLineAsync
                var readTask = _process.StandardOutput.ReadLineAsync();
                var delayTask = Task.Delay(Timeout.Infinite, cts.Token);

                var completed = await Task.WhenAny(readTask, delayTask);

                if (completed == delayTask)
                {
                    // 超时或取消
                    yield return $"[超时：命令执行超过 {timeoutMs}ms]";
                    yield break;
                }

                var line = await readTask;

                if (line == null)
                    break;

                if (line.TrimEnd() == "___COMMAND_END___")
                    break;

                yield return line;
            }

            LastActivityAt = DateTime.Now;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// 关闭会话
    /// </summary>
    public async Task CloseAsync()
    {
        if (_disposed || _process == null)
            return;

        try
        {
            await _writeLock.WaitAsync();
            try
            {
                if (!_process.HasExited)
                {
                    await _process.StandardInput.WriteLineAsync("exit");
                    await _process.StandardInput.FlushAsync();

                    if (!_process.WaitForExit(5000))
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                }
            }
            finally
            {
                _writeLock.Release();
            }

            _logger.LogInformation("会话已关闭: {SessionId}", Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "关闭会话失败: {SessionId}", Id);
            // 确保进程被终止
            try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); } catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await CloseAsync();
        _process?.Dispose();
        _writeLock.Dispose();
    }
}