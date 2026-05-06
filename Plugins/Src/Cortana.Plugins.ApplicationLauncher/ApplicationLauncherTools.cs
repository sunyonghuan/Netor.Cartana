using System.Text;
using Microsoft.Extensions.Logging;
using Netor.Cortana.Plugin.Native;

namespace Cortana.Plugins.ApplicationLauncher;

/// <summary>
/// 面向 AI 的应用启动工具集合。
/// </summary>
[Tool]
public sealed class ApplicationLauncherTools
{
    private readonly ApplicationLauncher _launcher;
    private readonly ILogger<ApplicationLauncherTools> _logger;

    /// <summary>
    /// 初始化应用启动工具集合。
    /// </summary>
    /// <param name="launcher">应用启动管理器。</param>
    /// <param name="logger">日志记录器。</param>
    public ApplicationLauncherTools(ApplicationLauncher launcher, ILogger<ApplicationLauncherTools> logger)
    {
        _launcher = launcher;
        _logger = logger;
    }

    /// <summary>
    /// 列出白名单中所有可识别应用及其安装状态。
    /// </summary>
    /// <returns>面向 AI 和用户阅读的应用列表文本。</returns>
    [Tool(Name = "list_launchable_applications", Description = "列出系统中所有可启动的应用程序，包括 IDE、浏览器、开发工具等。返回应用名称、路径、分类、可用状态等信息。")]
    public string ListLaunchableApplications()
    {
        try
        {
            _logger.LogInformation("列出可启动的应用");

            var applications = _launcher.GetLaunchableApplications();
            if (applications.Count == 0)
            {
                return "未找到可启动的应用。";
            }

            var result = new StringBuilder();
            result.AppendLine($"找到 {applications.Count} 个可启动的应用：\n");

            var groupedByCategory = applications.GroupBy(a => a.Category).OrderBy(g => g.Key);
            foreach (var categoryGroup in groupedByCategory)
            {
                result.AppendLine($"【{categoryGroup.Key}】");
                foreach (var app in categoryGroup)
                {
                    var status = app.IsAvailable ? "✓" : "✗";
                    result.AppendLine($"  {status} {app.Name,-20} {(app.IsAvailable ? app.Version : "未安装")}");
                }
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "列出应用失败");
            return $"✗ 错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 启动指定应用程序。
    /// </summary>
    /// <param name="applicationName">应用名称，必须在插件白名单中。</param>
    /// <param name="workingDirectory">可选工作目录。</param>
    /// <returns>启动结果文本。</returns>
    [Tool(Name = "launch_application", Description = "启动指定的应用程序。支持的应用包括 Visual Studio、VS Code、Chrome、Firefox、QQ、Notepad、Explorer 等。")]
    public string LaunchApplication(
        [Parameter(Description = "应用名称，例如 VS Code、Chrome、Notepad。必须在插件允许列表中。")]
        string applicationName,
        [Parameter(Description = "可选工作目录。为空时使用应用默认工作目录。")]
        string? workingDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(applicationName))
            {
                return "✗ 错误：应用名称不能为空";
            }

            _logger.LogInformation("启动应用：{ApplicationName}", applicationName);
            var result = _launcher.LaunchApplication(applicationName, workingDirectory);

            return result.Success
                ? $"✓ 成功启动应用 '{result.ApplicationName}'（进程ID: {result.ProcessId}，耗时: {result.ElapsedMs}ms）"
                : $"✗ 启动失败：{result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动应用失败：{ApplicationName}", applicationName);
            return $"✗ 错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 使用指定应用打开文件。
    /// </summary>
    /// <param name="filePath">要打开的文件完整路径。</param>
    /// <param name="applicationName">应用名称，必须在插件白名单中。</param>
    /// <returns>打开结果文本。</returns>
    [Tool(Name = "open_file_with_application", Description = "用指定的应用程序打开文件。例如：用 Visual Studio 打开 .cs 文件，用 Chrome 打开 .html 文件。")]
    public string OpenFileWithApplication(
        [Parameter(Description = "要打开的文件完整路径。")]
        string filePath,
        [Parameter(Description = "应用名称，例如 Visual Studio、VS Code、Chrome。必须在插件允许列表中。")]
        string applicationName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "✗ 错误：文件路径不能为空";
            }

            if (string.IsNullOrWhiteSpace(applicationName))
            {
                return "✗ 错误：应用名称不能为空";
            }

            _logger.LogInformation("用应用 {ApplicationName} 打开文件 {FilePath}", applicationName, filePath);
            var result = _launcher.OpenFileWithApplication(filePath, applicationName);

            return result.Success
                ? $"✓ 成功用 '{result.ApplicationName}' 打开文件（进程ID: {result.ProcessId}，耗时: {result.ElapsedMs}ms）\n文件：{result.FilePath}"
                : $"✗ 打开失败：{result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "用应用打开文件失败：{ApplicationName} {FilePath}", applicationName, filePath);
            return $"✗ 错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 查询指定应用的详细信息。
    /// </summary>
    /// <param name="applicationName">应用名称。</param>
    /// <returns>应用详情文本。</returns>
    [Tool(Name = "get_application_info", Description = "查询指定应用的详细信息，包括路径、版本、分类等。")]
    public string GetApplicationInfo(
        [Parameter(Description = "应用名称，例如 VS Code、Chrome、Notepad。")]
        string applicationName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(applicationName))
            {
                return "✗ 错误：应用名称不能为空";
            }

            _logger.LogInformation("查询应用详情：{ApplicationName}", applicationName);
            var appInfo = _launcher.FindApplication(applicationName);

            if (appInfo is null)
            {
                return $"✗ 未找到应用 '{applicationName}'";
            }

            if (!appInfo.IsAvailable)
            {
                return $"✗ 应用 '{applicationName}' 未安装\n预期路径：{appInfo.ExecutablePath}";
            }

            var result = new StringBuilder();
            result.AppendLine("【应用信息】");
            result.AppendLine($"名称：{appInfo.Name}");
            result.AppendLine($"分类：{appInfo.Category}");
            result.AppendLine($"路径：{appInfo.ExecutablePath}");
            result.AppendLine($"版本：{appInfo.Version}");
            result.AppendLine($"描述：{appInfo.Description}");
            result.AppendLine($"状态：{(appInfo.IsAvailable ? "✓ 可用" : "✗ 不可用")}");

            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取应用详情失败：{ApplicationName}", applicationName);
            return $"✗ 错误：{ex.Message}";
        }
    }
}
