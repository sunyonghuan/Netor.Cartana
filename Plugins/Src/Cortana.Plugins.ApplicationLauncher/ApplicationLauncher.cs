using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ProcessDiag = System.Diagnostics.Process;

namespace Cortana.Plugins.ApplicationLauncher;

/// <summary>
/// 应用启动管理器，负责基于白名单发现、验证和启动本机应用程序。
/// </summary>
public sealed class ApplicationLauncher
{
    private readonly ILogger<ApplicationLauncher> _logger;

    /// <summary>
    /// 应用启动白名单。键为用户友好的应用名称，值为可执行文件名或常见安装路径模式。
    /// </summary>
    private static readonly Dictionary<string, string[]> ApplicationWhitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Visual Studio", ["devenv.exe", @"C:\Program Files\Microsoft Visual Studio\*\Enterprise\Common7\IDE\devenv.exe", @"C:\Program Files (x86)\Microsoft Visual Studio\*\Enterprise\Common7\IDE\devenv.exe"] },
        { "VS Code", ["code.exe", @"C:\Users\*\AppData\Local\Programs\Microsoft VS Code\Code.exe"] },
        { "Visual Studio Code", ["code.exe", @"C:\Users\*\AppData\Local\Programs\Microsoft VS Code\Code.exe"] },

        { "Chrome", ["chrome.exe", @"C:\Program Files\Google\Chrome\Application\chrome.exe", @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"] },
        { "Firefox", ["firefox.exe", @"C:\Program Files\Mozilla Firefox\firefox.exe", @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe"] },
        { "Edge", ["msedge.exe", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"] },

        { "Git Bash", ["bash.exe", @"C:\Program Files\Git\bin\bash.exe"] },
        { "PowerShell", ["powershell.exe", "pwsh.exe"] },
        { "Notepad++", ["notepad++.exe", @"C:\Program Files\Notepad++\notepad++.exe", @"C:\Program Files (x86)\Notepad++\notepad++.exe"] },

        { "QQ", ["QQ.exe", @"C:\Program Files\Tencent\QQ\*\QQ.exe", @"C:\Program Files (x86)\Tencent\QQ\*\QQ.exe"] },
        { "WeChat", ["WeChat.exe", @"C:\Program Files\Tencent\WeChat\*\WeChat.exe", @"C:\Program Files (x86)\Tencent\WeChat\*\WeChat.exe"] },

        { "Notepad", ["notepad.exe"] },
        { "Paint", ["mspaint.exe"] },
        { "Calculator", ["calc.exe"] },
        { "Explorer", ["explorer.exe"] },
    };

    /// <summary>
    /// 初始化应用启动管理器。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public ApplicationLauncher(ILogger<ApplicationLauncher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取白名单中所有应用的可用状态信息。
    /// </summary>
    /// <returns>应用信息列表，包含已安装和未安装的应用。</returns>
    public List<ApplicationInfo> GetLaunchableApplications()
    {
        var result = new List<ApplicationInfo>();

        foreach (var (appName, pathPatterns) in ApplicationWhitelist)
        {
            var appInfo = FindApplication(appName, pathPatterns);
            if (appInfo is not null)
            {
                result.Add(appInfo);
            }
        }

        _logger.LogInformation("发现 {Count} 个可启动的应用", result.Count);
        return result;
    }

    /// <summary>
    /// 根据应用名称查找应用信息。
    /// </summary>
    /// <param name="applicationName">白名单中的应用名称。</param>
    /// <returns>找到的应用信息；如果名称不在白名单中则返回 <see langword="null" />。</returns>
    public ApplicationInfo? FindApplication(string applicationName)
    {
        if (!ApplicationWhitelist.TryGetValue(applicationName, out var pathPatterns))
        {
            _logger.LogWarning("应用 {AppName} 不在白名单中", applicationName);
            return null;
        }

        return FindApplication(applicationName, pathPatterns);
    }

    /// <summary>
    /// 启动指定应用。
    /// </summary>
    /// <param name="applicationName">白名单中的应用名称。</param>
    /// <param name="workingDirectory">可选工作目录。</param>
    /// <returns>应用启动结果。</returns>
    public ApplicationLaunchResult LaunchApplication(string applicationName, string? workingDirectory = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ApplicationLaunchResult
        {
            ApplicationName = applicationName,
            LaunchTime = DateTime.Now
        };

        try
        {
            var appInfo = FindApplication(applicationName);
            if (appInfo is null)
            {
                result.Success = false;
                result.ErrorMessage = $"未找到应用 '{applicationName}' 或应用不在允许列表中";
                _logger.LogWarning("{ErrorMessage}", result.ErrorMessage);
                return result;
            }

            if (!appInfo.IsAvailable)
            {
                result.Success = false;
                result.ErrorMessage = $"应用 '{applicationName}' 的执行文件不存在：{appInfo.ExecutablePath}";
                _logger.LogWarning("{ErrorMessage}", result.ErrorMessage);
                return result;
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = appInfo.ExecutablePath,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
            {
                processInfo.WorkingDirectory = workingDirectory;
            }

            var process = ProcessDiag.Start(processInfo);
            if (process is null)
            {
                result.Success = false;
                result.ErrorMessage = $"无法启动应用 '{applicationName}'";
                _logger.LogError("{ErrorMessage}", result.ErrorMessage);
                return result;
            }

            result.Success = true;
            result.ProcessId = process.Id;
            _logger.LogInformation("成功启动应用 {AppName}，进程ID: {ProcessId}", applicationName, process.Id);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "启动应用 {AppName} 时出错", applicationName);
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    /// <summary>
    /// 使用指定应用打开文件。
    /// </summary>
    /// <param name="filePath">要打开的文件完整路径。</param>
    /// <param name="applicationName">白名单中的应用名称。</param>
    /// <returns>打开文件操作结果。</returns>
    public OpenFileResult OpenFileWithApplication(string filePath, string applicationName)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new OpenFileResult
        {
            ApplicationName = applicationName,
            FilePath = filePath
        };

        try
        {
            if (!File.Exists(filePath))
            {
                result.Success = false;
                result.ErrorMessage = $"文件不存在：{filePath}";
                _logger.LogWarning("{ErrorMessage}", result.ErrorMessage);
                return result;
            }

            var appInfo = FindApplication(applicationName);
            if (appInfo is null)
            {
                result.Success = false;
                result.ErrorMessage = $"未找到应用 '{applicationName}' 或应用不在允许列表中";
                _logger.LogWarning("{ErrorMessage}", result.ErrorMessage);
                return result;
            }

            if (!appInfo.IsAvailable)
            {
                result.Success = false;
                result.ErrorMessage = $"应用的执行文件不存在：{appInfo.ExecutablePath}";
                _logger.LogWarning("{ErrorMessage}", result.ErrorMessage);
                return result;
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = appInfo.ExecutablePath,
                Arguments = $"\"{filePath}\"",
                UseShellExecute = true,
                CreateNoWindow = false
            };

            var process = ProcessDiag.Start(processInfo);
            if (process is null)
            {
                result.Success = false;
                result.ErrorMessage = $"无法用应用 '{applicationName}' 打开文件";
                _logger.LogError("{ErrorMessage}", result.ErrorMessage);
                return result;
            }

            result.Success = true;
            result.ProcessId = process.Id;
            _logger.LogInformation("成功用应用 {AppName} 打开文件 {FilePath}，进程ID: {ProcessId}", applicationName, filePath, process.Id);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "用应用 {AppName} 打开文件 {FilePath} 时出错", applicationName, filePath);
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    /// <summary>
    /// 根据路径模式集合解析指定应用的实际安装位置。
    /// </summary>
    /// <param name="applicationName">应用名称。</param>
    /// <param name="pathPatterns">可执行文件名或安装路径模式。</param>
    /// <returns>应用信息；如果所有路径均不可用，则返回不可用状态的应用信息。</returns>
    private ApplicationInfo? FindApplication(string applicationName, string[] pathPatterns)
    {
        foreach (var pattern in pathPatterns)
        {
            var executablePath = ResolveExecutablePath(pattern);
            if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
            {
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);

                    return new ApplicationInfo
                    {
                        Name = applicationName,
                        ExecutablePath = executablePath,
                        Description = versionInfo.FileDescription ?? string.Empty,
                        Version = versionInfo.FileVersion ?? string.Empty,
                        IconPath = executablePath,
                        IsAvailable = true,
                        IsLaunchable = true,
                        Category = DetermineCategory(applicationName)
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "获取应用信息失败：{Path}", executablePath);
                }
            }
        }

        return new ApplicationInfo
        {
            Name = applicationName,
            ExecutablePath = pathPatterns.FirstOrDefault() ?? string.Empty,
            IsAvailable = false,
            IsLaunchable = false,
            Category = DetermineCategory(applicationName)
        };
    }

    /// <summary>
    /// 解析可执行文件路径，支持环境变量和简单通配符。
    /// </summary>
    /// <param name="pattern">可执行文件名或路径模式。</param>
    /// <returns>解析后的可执行文件路径；解析失败时返回 <see langword="null" />。</returns>
    private string? ResolveExecutablePath(string pattern)
    {
        var expandedPath = Environment.ExpandEnvironmentVariables(pattern);

        if (!expandedPath.Contains('*') && !expandedPath.Contains('?'))
        {
            return expandedPath;
        }

        try
        {
            var directory = Path.GetDirectoryName(expandedPath);
            var searchPattern = Path.GetFileName(expandedPath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return null;
            }

            var files = Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
            return files.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "解析路径失败：{Pattern}", pattern);
            return null;
        }
    }

    /// <summary>
    /// 根据应用名称推断应用分类。
    /// </summary>
    /// <param name="applicationName">应用名称。</param>
    /// <returns>应用分类名称。</returns>
    private static string DetermineCategory(string applicationName)
    {
        return applicationName switch
        {
            "Visual Studio" or "VS Code" or "Visual Studio Code" or "Notepad++" => "IDE",
            "Chrome" or "Firefox" or "Edge" => "Browser",
            "QQ" or "WeChat" => "Communication",
            "Git Bash" or "PowerShell" => "Development",
            _ => "Utility"
        };
    }
}
