using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Netor.Cortana.Plugin.BuiltIn.WindowManagement;

/// <summary>
/// 窗口管理 AI 工具提供者
/// </summary>
public sealed class WindowManagerProvider : AIContextProvider
{
    private readonly ILogger<WindowManagerProvider> _logger;
    private readonly WindowManager _windowManager;
    private readonly List<AITool> _tools = [];

    public WindowManagerProvider(ILogger<WindowManagerProvider> logger, WindowManager windowManager)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(windowManager);

        _logger = logger;
        _windowManager = windowManager;
    }

    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        if (_tools.Count == 0)
        {
            RegisterTools();
        }

        return new ValueTask<AIContext>(new AIContext { Tools = _tools });
    }

    private void RegisterTools()
    {
        // 工具1：列出所有窗口
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_list_windows",
            description: "列出系统中所有打开的窗口，返回窗口标题、进程名、位置、状态等信息。",
            method: ListWindowsAsync));

        // 工具2：获取活跃窗口
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_get_active_window",
            description: "获取当前活跃（获得焦点）的窗口信息。",
            method: GetActiveWindowAsync));

        // 工具3：按标题查找窗口
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_find_window_by_title",
            description: "按标题查找窗口。支持模糊匹配。",
            method: FindWindowByTitleAsync));

        // 工具4：激活窗口
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_activate_window",
            description: "激活（聚焦）指定的窗口。参数可以是窗口句柄（如 0x12345）或窗口标题。",
            method: ActivateWindowAsync));

        // 工具5：最小化窗口
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_minimize_window",
            description: "最小化指定的窗口。",
            method: MinimizeWindowAsync));

        // 工具6：最大化窗口
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_maximize_window",
            description: "最大化指定的窗口。",
            method: MaximizeWindowAsync));

        // 工具7：恢复窗口
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_restore_window",
            description: "恢复窗口到正常大小（如果已最小化或最大化）。",
            method: RestoreWindowAsync));

        // 工具8：关闭窗口
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_close_window",
            description: "关闭指定的窗口。",
            method: CloseWindowAsync));

        // 工具9：移动和调整窗口
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_move_window",
            description: "移动和调整窗口的位置和大小。",
            method: MoveWindowAsync));
    }

    private async Task<string> ListWindowsAsync(CancellationToken ct = default)
    {
        try
        {
            var windows = _windowManager.ListAllWindows();

            if (windows.Count == 0)
                return "没有找到打开的窗口。";

            var result = new System.Text.StringBuilder();
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

    private async Task<string> GetActiveWindowAsync(CancellationToken ct = default)
    {
        try
        {
            var window = _windowManager.GetActiveWindow();

            if (window == null)
                return "✗ 无法获取活跃窗口";

            return $@"✓ 活跃窗口：
标题: {window.Title}
进程: {window.ProcessName} (PID: {window.ProcessId})
句柄: {window.Hwnd}
位置: ({window.Rect.X}, {window.Rect.Y})
大小: {window.Rect.Width}x{window.Rect.Height}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取活跃窗口失败");
            return $"✗ 错误：{ex.Message}";
        }
    }

    private async Task<string> FindWindowByTitleAsync(string title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "✗ 错误：标题不能为空";

        try
        {
            var window = _windowManager.GetWindowByTitle(title);

            if (window == null)
                return $"✗ 找不到标题包含 '{title}' 的窗口";

            return $@"✓ 找到窗口：
标题: {window.Title}
进程: {window.ProcessName} (PID: {window.ProcessId})
句柄: {window.Hwnd}
位置: ({window.Rect.X}, {window.Rect.Y})
大小: {window.Rect.Width}x{window.Rect.Height}
状态: {(window.IsMinimized ? "最小化" : window.IsMaximized ? "最大化" : "正常")}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查找窗口失败: {Title}", title);
            return $"✗ 错误：{ex.Message}";
        }
    }

    private async Task<bool> ActivateWindowAsync(string hWndOrTitle, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hWndOrTitle))
            return false;

        try
        {
            var result = _windowManager.ActivateWindow(hWndOrTitle);
            if (result)
            {
                _logger.LogInformation("窗口已激活: {HwndOrTitle}", hWndOrTitle);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "激活窗口失败: {HwndOrTitle}", hWndOrTitle);
            return false;
        }
    }

    private async Task<bool> MinimizeWindowAsync(string hWndOrTitle, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hWndOrTitle))
            return false;

        try
        {
            var result = _windowManager.MinimizeWindow(hWndOrTitle);
            if (result)
            {
                _logger.LogInformation("窗口已最小化: {HwndOrTitle}", hWndOrTitle);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "最小化窗口失败: {HwndOrTitle}", hWndOrTitle);
            return false;
        }
    }

    private async Task<bool> MaximizeWindowAsync(string hWndOrTitle, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hWndOrTitle))
            return false;

        try
        {
            var result = _windowManager.MaximizeWindow(hWndOrTitle);
            if (result)
            {
                _logger.LogInformation("窗口已最大化: {HwndOrTitle}", hWndOrTitle);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "最大化窗口失败: {HwndOrTitle}", hWndOrTitle);
            return false;
        }
    }

    private async Task<bool> RestoreWindowAsync(string hWndOrTitle, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hWndOrTitle))
            return false;

        try
        {
            var result = _windowManager.RestoreWindow(hWndOrTitle);
            if (result)
            {
                _logger.LogInformation("窗口已恢复: {HwndOrTitle}", hWndOrTitle);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复窗口失败: {HwndOrTitle}", hWndOrTitle);
            return false;
        }
    }

    private async Task<bool> CloseWindowAsync(string hWndOrTitle, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hWndOrTitle))
            return false;

        try
        {
            var result = _windowManager.CloseWindow(hWndOrTitle);
            if (result)
            {
                _logger.LogInformation("关闭窗口: {HwndOrTitle}", hWndOrTitle);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "关闭窗口失败: {HwndOrTitle}", hWndOrTitle);
            return false;
        }
    }

    private async Task<bool> MoveWindowAsync(
        string hWndOrTitle,
        int x,
        int y,
        int width,
        int height,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hWndOrTitle) || width <= 0 || height <= 0)
            return false;

        try
        {
            var result = _windowManager.MoveWindowPosition(hWndOrTitle, x, y, width, height);
            if (result)
            {
                _logger.LogInformation("窗口已移动: {HwndOrTitle} -> ({X}, {Y}, {Width}, {Height})",
                    hWndOrTitle, x, y, width, height);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移动窗口失败: {HwndOrTitle}", hWndOrTitle);
            return false;
        }
    }
}
