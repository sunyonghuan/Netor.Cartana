using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ProcessDiag = System.Diagnostics.Process;

namespace Netor.Cortana.Plugin.BuiltIn.ApplicationLauncher;

/// <summary>
/// 应用启动管理器，负责发现、验证和启动应用程序
/// </summary>
public class ApplicationLauncher
{
    private readonly ILogger<ApplicationLauncher> _logger;

    // 应用白名单：应用友好名称 -> 执行路径（支持通配符查询）
    private static readonly Dictionary<string, string[]> ApplicationWhitelist = new()
    {
        // IDE
        { "Visual Studio", new[] { "devenv.exe", @"C:\Program Files\Microsoft Visual Studio\*\Enterprise\Common7\IDE\devenv.exe", @"C:\Program Files (x86)\Microsoft Visual Studio\*\Enterprise\Common7\IDE\devenv.exe" } },
        { "VS Code", new[] { "code.exe", @"C:\Users\*\AppData\Local\Programs\Microsoft VS Code\Code.exe" } },
        { "Visual Studio Code", new[] { "code.exe", @"C:\Users\*\AppData\Local\Programs\Microsoft VS Code\Code.exe" } },

        // 浏览器
        { "Chrome", new[] { "chrome.exe", @"C:\Program Files\Google\Chrome\Application\chrome.exe", @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe" } },
        { "Firefox", new[] { "firefox.exe", @"C:\Program Files\Mozilla Firefox\firefox.exe", @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe" } },
        { "Edge", new[] { "msedge.exe", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" } },

        // 开发工具
        { "Git Bash", new[] { "bash.exe", @"C:\Program Files\Git\bin\bash.exe" } },
        { "PowerShell", new[] { "powershell.exe", "pwsh.exe" } },
        { "Notepad++", new[] { "notepad++.exe", @"C:\Program Files\Notepad++\notepad++.exe", @"C:\Program Files (x86)\Notepad++\notepad++.exe" } },

        // 通讯工具
        { "QQ", new[] { "QQ.exe", @"C:\Program Files\Tencent\QQ\*\QQ.exe", @"C:\Program Files (x86)\Tencent\QQ\*\QQ.exe" } },
        { "WeChat", new[] { "WeChat.exe", @"C:\Program Files\Tencent\WeChat\*\WeChat.exe", @"C:\Program Files (x86)\Tencent\WeChat\*\WeChat.exe" } },

        // 系统工具
        { "Notepad", new[] { "notepad.exe" } },
        { "Paint", new[] { "mspaint.exe" } },
        { "Calculator", new[] { "calc.exe" } },
        { "Explorer", new[] { "explorer.exe" } },
    };

    public ApplicationLauncher(ILogger<ApplicationLauncher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 列出所有可启动的应用
    /// </summary>
    public List<ApplicationInfo> GetLaunchableApplications()
    {
        var result = new List<ApplicationInfo>();

        foreach (var (appName, pathPatterns) in ApplicationWhitelist)
        {
            var appInfo = FindApplication(appName, pathPatterns);
            if (appInfo != null)
            {
                result.Add(appInfo);
            }
        }

        _logger.LogInformation("发现 {Count} 个可启动的应用", result.Count);
        return result;
    }

    /// <summary>
    /// 查找指定的应用
    /// </summary>
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
    /// 启动指定的应用
    /// </summary>
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
            // 查找应用
            var appInfo = FindApplication(applicationName);
            if (appInfo == null)
            {
                result.Success = false;
                result.ErrorMessage = $"未找到应用 '{applicationName}' 或应用不在允许列表中";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }

            if (!appInfo.IsAvailable)
            {
                result.Success = false;
                result.ErrorMessage = $"应用 '{applicationName}' 的执行文件不存在：{appInfo.ExecutablePath}";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }

            // 启动进程
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
            if (process == null)
            {
                result.Success = false;
                result.ErrorMessage = $"无法启动应用 '{applicationName}'";
                _logger.LogError(result.ErrorMessage);
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
    /// 用指定应用打开文件
    /// </summary>
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
            // 验证文件存在
            if (!File.Exists(filePath))
            {
                result.Success = false;
                result.ErrorMessage = $"文件不存在：{filePath}";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }

            // 查找应用
            var appInfo = FindApplication(applicationName);
            if (appInfo == null)
            {
                result.Success = false;
                result.ErrorMessage = $"未找到应用 '{applicationName}' 或应用不在允许列表中";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }

            if (!appInfo.IsAvailable)
            {
                result.Success = false;
                result.ErrorMessage = $"应用的执行文件不存在：{appInfo.ExecutablePath}";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }

            // 启动进程并传入文件路径
            var processInfo = new ProcessStartInfo
            {
                FileName = appInfo.ExecutablePath,
                Arguments = $"\"{filePath}\"",
                UseShellExecute = true,
                CreateNoWindow = false
            };

            var process = ProcessDiag.Start(processInfo);
            if (process == null)
            {
                result.Success = false;
                result.ErrorMessage = $"无法用应用 '{applicationName}' 打开文件";
                _logger.LogError(result.ErrorMessage);
                return result;
            }

            result.Success = true;
            result.ProcessId = process.Id;
            _logger.LogInformation("成功用应用 {AppName} 打开文件 {FilePath}，进程ID: {ProcessId}",
                applicationName, filePath, process.Id);
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
    /// 根据应用名称和路径模式查找应用
    /// </summary>
    private ApplicationInfo? FindApplication(string applicationName, string[] pathPatterns)
    {
        foreach (var pattern in pathPatterns)
        {
            var executablePath = ResolveExecutablePath(pattern);
            if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
            {
                try
                {
                    var fileInfo = new FileInfo(executablePath);
                    var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);

                    return new ApplicationInfo
                    {
                        Name = applicationName,
                        ExecutablePath = executablePath,
                        Description = versionInfo.FileDescription ?? "",
                        Version = versionInfo.FileVersion ?? "",
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

        // 如果没有找到，返回一个"不可用"的应用信息
        return new ApplicationInfo
        {
            Name = applicationName,
            ExecutablePath = pathPatterns.FirstOrDefault() ?? "",
            IsAvailable = false,
            IsLaunchable = false,
            Category = DetermineCategory(applicationName)
        };
    }

    /// <summary>
    /// 解析执行路径（处理通配符和环境变量）
    /// </summary>
    private string? ResolveExecutablePath(string pattern)
    {
        // 展开环境变量
        var expandedPath = Environment.ExpandEnvironmentVariables(pattern);

        // 如果没有通配符，直接返回
        if (!expandedPath.Contains('*') && !expandedPath.Contains('?'))
        {
            return expandedPath;
        }

        // 处理通配符
        try
        {
            var directory = Path.GetDirectoryName(expandedPath);
            var searchPattern = Path.GetFileName(expandedPath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return null;

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
    /// 根据应用名称判断分类
    /// </summary>
    private string DetermineCategory(string applicationName)
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
