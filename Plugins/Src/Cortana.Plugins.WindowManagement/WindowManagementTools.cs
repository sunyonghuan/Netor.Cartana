using System.Text;
using Microsoft.Extensions.Logging;
using Netor.Cortana.Plugin.Native;

namespace Cortana.Plugins.WindowManagement;

/// <summary>
/// 面向 AI 的 Windows 窗口管理工具集合。
/// </summary>
[Tool]
public sealed class WindowManagementTools
{
    private readonly ILogger<WindowManagementTools> _logger;
    private readonly WindowManager _windowManager;

    /// <summary>
    /// 初始化窗口管理工具集合。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="windowManager">窗口管理器。</param>
    public WindowManagementTools(ILogger<WindowManagementTools> logger, WindowManager windowManager)
    {
        _logger = logger;
        _windowManager = windowManager;
    }

    /// <summary>
    /// 列出系统中所有可识别的顶层窗口。
    /// </summary>
    /// <returns>窗口列表文本。</returns>
    [Tool(Name = "list_windows", Description = "列出系统中所有打开的窗口，返回窗口标题、进程名、位置、状态等信息。")]
    public string ListWindows()
    {
        try
        {
            var windows = _windowManager.ListAllWindows();
            if (windows.Count == 0)
            {
                return "没有找到打开的窗口。";
            }

            var result = new StringBuilder();
            result.AppendLine($"✓ 找到 {windows.Count} 个窗口：\n");

            foreach (var window in windows.OrderByDescending(w => w.IsActive))
            {
                var activeIndicator = window.IsActive ? "●" : "○";
                result.AppendLine($"[{activeIndicator}] {window.Title}");
                result.AppendLine($"    进程: {window.ProcessName} (PID: {window.ProcessId})");
                result.AppendLine($"    句柄: {window.Hwnd}");
                result.AppendLine($"    位置: ({window.Rect.X}, {window.Rect.Y})");
                result.AppendLine($"    大小: {window.Rect.Width}x{window.Rect.Height}");
                result.AppendLine($"    状态: {(window.IsMinimized ? "最小化" : window.IsMaximized ? "最大化" : "正常")} | {(window.IsVisible ? "可见" : "隐藏")}");
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "列出窗口失败");
            return $"✗ 错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 获取当前前台活跃窗口信息。
    /// </summary>
    /// <returns>活跃窗口详情文本。</returns>
    [Tool(Name = "get_active_window", Description = "获取当前活跃（获得焦点）的窗口信息。")]
    public string GetActiveWindow()
    {
        try
        {
            var window = _windowManager.GetActiveWindow();
            if (window is null)
            {
                return "✗ 无法获取活跃窗口";
            }

            return $"""
                ✓ 活跃窗口：
                标题: {window.Title}
                进程: {window.ProcessName} (PID: {window.ProcessId})
                句柄: {window.Hwnd}
                位置: ({window.Rect.X}, {window.Rect.Y})
                大小: {window.Rect.Width}x{window.Rect.Height}
                """;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取活跃窗口失败");
            return $"✗ 错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 根据标题查找窗口。
    /// </summary>
    /// <param name="title">窗口标题或标题片段。</param>
    /// <returns>匹配窗口详情文本。</returns>
    [Tool(Name = "find_window_by_title", Description = "按标题查找窗口。支持模糊匹配。")]
    public string FindWindowByTitle(
        [Parameter(Description = "窗口标题或标题片段。")]
        string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "✗ 错误：标题不能为空";
        }

        try
        {
            var window = _windowManager.GetWindowByTitle(title);
            if (window is null)
            {
                return $"✗ 找不到标题包含 '{title}' 的窗口";
            }

            return $"""
                ✓ 找到窗口：
                标题: {window.Title}
                进程: {window.ProcessName} (PID: {window.ProcessId})
                句柄: {window.Hwnd}
                位置: ({window.Rect.X}, {window.Rect.Y})
                大小: {window.Rect.Width}x{window.Rect.Height}
                状态: {(window.IsMinimized ? "最小化" : window.IsMaximized ? "最大化" : "正常")}
                """;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查找窗口失败: {Title}", title);
            return $"✗ 错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 激活指定窗口。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄或窗口标题。</param>
    /// <returns>是否成功发起激活操作。</returns>
    [Tool(Name = "activate_window", Description = "激活（聚焦）指定的窗口。参数可以是窗口句柄（如 0x12345）或窗口标题。")]
    public bool ActivateWindow([Parameter(Description = "窗口句柄或窗口标题。")]
        string hWndOrTitle)
    {
        return ExecuteWindowAction(hWndOrTitle, _windowManager.ActivateWindow, "激活窗口", "窗口已激活");
    }

    /// <summary>
    /// 最小化指定窗口。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄或窗口标题。</param>
    /// <returns>是否成功发起最小化操作。</returns>
    [Tool(Name = "minimize_window", Description = "最小化指定的窗口。")]
    public bool MinimizeWindow([Parameter(Description = "窗口句柄或窗口标题。")]
        string hWndOrTitle)
    {
        return ExecuteWindowAction(hWndOrTitle, _windowManager.MinimizeWindow, "最小化窗口", "窗口已最小化");
    }

    /// <summary>
    /// 最大化指定窗口。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄或窗口标题。</param>
    /// <returns>是否成功发起最大化操作。</returns>
    [Tool(Name = "maximize_window", Description = "最大化指定的窗口。")]
    public bool MaximizeWindow([Parameter(Description = "窗口句柄或窗口标题。")]
        string hWndOrTitle)
    {
        return ExecuteWindowAction(hWndOrTitle, _windowManager.MaximizeWindow, "最大化窗口", "窗口已最大化");
    }

    /// <summary>
    /// 恢复指定窗口到普通大小。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄或窗口标题。</param>
    /// <returns>是否成功发起恢复操作。</returns>
    [Tool(Name = "restore_window", Description = "恢复窗口到正常大小（如果已最小化或最大化）。")]
    public bool RestoreWindow([Parameter(Description = "窗口句柄或窗口标题。")]
        string hWndOrTitle)
    {
        return ExecuteWindowAction(hWndOrTitle, _windowManager.RestoreWindow, "恢复窗口", "窗口已恢复");
    }

    /// <summary>
    /// 关闭指定窗口。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄或窗口标题。</param>
    /// <returns>是否成功发送关闭消息。</returns>
    [Tool(Name = "close_window", Description = "关闭指定的窗口。")]
    public bool CloseWindow([Parameter(Description = "窗口句柄或窗口标题。")]
        string hWndOrTitle)
    {
        return ExecuteWindowAction(hWndOrTitle, _windowManager.CloseWindow, "关闭窗口", "关闭窗口");
    }

    /// <summary>
    /// 移动并调整指定窗口大小。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄或窗口标题。</param>
    /// <param name="x">目标 X 坐标。</param>
    /// <param name="y">目标 Y 坐标。</param>
    /// <param name="width">目标宽度。</param>
    /// <param name="height">目标高度。</param>
    /// <returns>是否成功发起移动操作。</returns>
    [Tool(Name = "move_window", Description = "移动和调整窗口的位置和大小。")]
    public bool MoveWindow(
        [Parameter(Description = "窗口句柄或窗口标题。")]
        string hWndOrTitle,
        [Parameter(Description = "目标 X 坐标。")]
        int x,
        [Parameter(Description = "目标 Y 坐标。")]
        int y,
        [Parameter(Description = "目标宽度，必须大于 0。")]
        int width,
        [Parameter(Description = "目标高度，必须大于 0。")]
        int height)
    {
        if (string.IsNullOrWhiteSpace(hWndOrTitle) || width <= 0 || height <= 0)
        {
            return false;
        }

        try
        {
            var result = _windowManager.MoveWindowPosition(hWndOrTitle, x, y, width, height);
            if (result)
            {
                _logger.LogInformation("窗口已移动: {HwndOrTitle} -> ({X}, {Y}, {Width}, {Height})", hWndOrTitle, x, y, width, height);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移动窗口失败: {HwndOrTitle}", hWndOrTitle);
            return false;
        }
    }

    /// <summary>
    /// 执行通用窗口控制动作，并统一记录日志和处理异常。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄或窗口标题。</param>
    /// <param name="action">实际执行的窗口动作。</param>
    /// <param name="actionName">动作名称，用于错误日志。</param>
    /// <param name="successMessage">成功日志消息。</param>
    /// <returns>窗口动作是否执行成功。</returns>
    private bool ExecuteWindowAction(string hWndOrTitle, Func<string, bool> action, string actionName, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(hWndOrTitle))
        {
            return false;
        }

        try
        {
            var result = action(hWndOrTitle);
            if (result)
            {
                _logger.LogInformation("{SuccessMessage}: {HwndOrTitle}", successMessage, hWndOrTitle);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ActionName}失败: {HwndOrTitle}", actionName, hWndOrTitle);
            return false;
        }
    }
}
