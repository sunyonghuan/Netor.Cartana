using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Netor.Cortana.Plugin.BuiltIn.ApplicationLauncher;

/// <summary>
/// 应用启动 AI 工具提供者
/// 向 AI 提供应用发现、启动和文件打开的功能
/// </summary>
public class ApplicationLauncherProvider : AIContextProvider
{
    private readonly ApplicationLauncher _launcher;
    private readonly ILogger<ApplicationLauncherProvider> _logger;
    private readonly List<AIFunction> _tools = [];

    public ApplicationLauncherProvider(ApplicationLauncher launcher, ILogger<ApplicationLauncherProvider> logger)
    {
        _launcher = launcher;
        _logger = logger;
        RegisterTools();
    }

    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        return new ValueTask<AIContext>(new AIContext { Tools = _tools });
    }

    private void RegisterTools()
    {
        // 工具1：列出所有可启动的应用
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_list_launchable_applications",
            description: "列出系统中所有可启动的应用程序，包括IDE、浏览器、开发工具等。返回应用名称、路径、分类、可用状态等信息。",
            method: ListLaunchableApplicationsAsync));

        // 工具2：启动指定应用
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_launch_application",
            description: "启动指定的应用程序。支持的应用包括Visual Studio、VS Code、Chrome、Firefox、QQ等。",
            method: LaunchApplicationAsync));

        // 工具3：用指定应用打开文件
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_open_file_with_application",
            description: "用指定的应用程序打开文件。例如：用Visual Studio打开.cs文件，用Chrome打开.html文件。",
            method: OpenFileWithApplicationAsync));

        // 工具4：查询应用详情
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_get_application_info",
            description: "查询指定应用的详细信息，包括路径、版本、分类等。",
            method: GetApplicationInfoAsync));
    }

    /// <summary>
    /// 工具1：列出可启动的应用
    /// </summary>
    private async Task<string> ListLaunchableApplicationsAsync()
    {
        try
        {
            _logger.LogInformation("列出可启动的应用");

            var applications = _launcher.GetLaunchableApplications();

            if (applications.Count == 0)
            {
                return "未找到可启动的应用。";
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine($"找到 {applications.Count} 个可启动的应用：\n");

            // 按分类分组显示
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
    /// 工具2：启动应用
    /// </summary>
    private async Task<string> LaunchApplicationAsync(
        string applicationName,
        string? workingDirectory = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(applicationName))
            {
                return "✗ 错误：应用名称不能为空";
            }

            _logger.LogInformation("启动应用：{ApplicationName}", applicationName);

            var result = _launcher.LaunchApplication(applicationName, workingDirectory);

            if (result.Success)
            {
                return $"✓ 成功启动应用 '{result.ApplicationName}'（进程ID: {result.ProcessId}，耗时: {result.ElapsedMs}ms）";
            }
            else
            {
                return $"✗ 启动失败：{result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动应用失败：{ApplicationName}", applicationName);
            return $"✗ 错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 工具3：用应用打开文件
    /// </summary>
    private async Task<string> OpenFileWithApplicationAsync(
        string filePath,
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

            if (result.Success)
            {
                return $"✓ 成功用 '{result.ApplicationName}' 打开文件（进程ID: {result.ProcessId}，耗时: {result.ElapsedMs}ms）\n文件：{result.FilePath}";
            }
            else
            {
                return $"✗ 打开失败：{result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "用应用打开文件失败：{ApplicationName} {FilePath}", applicationName, filePath);
            return $"✗ 错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 工具4：获取应用详情
    /// </summary>
    private async Task<string> GetApplicationInfoAsync(string applicationName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(applicationName))
            {
                return "✗ 错误：应用名称不能为空";
            }

            _logger.LogInformation("查询应用详情：{ApplicationName}", applicationName);

            var appInfo = _launcher.FindApplication(applicationName);

            if (appInfo == null)
            {
                return $"✗ 未找到应用 '{applicationName}'";
            }

            if (!appInfo.IsAvailable)
            {
                return $"✗ 应用 '{applicationName}' 未安装\n预期路径：{appInfo.ExecutablePath}";
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine($"【应用信息】");
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
