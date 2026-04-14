using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Netor.Cortana.Plugin.BuiltIn.WindowManagement;

/// <summary>
/// 窗口管理器 - 使用 Windows API 管理窗口
/// </summary>
public sealed class WindowManager
{
    // Windows API P/Invoke
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    private static extern bool SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    // ShowWindow 参数
    private const int SW_MINIMIZE = 6;

    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;
    private const int SW_CLOSE = 0;
    private const int WM_CLOSE = 0x0010;

    /// <summary>
    /// 窗口枚举回调
    /// </summary>
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// 列出所有窗口
    /// </summary>
    public List<WindowInfo> ListAllWindows()
    {
        var windows = new List<WindowInfo>();

        EnumWindows((hWnd, lParam) =>
        {
            try
            {
                var windowInfo = GetWindowInfo(hWnd);
                if (windowInfo != null)
                {
                    windows.Add(windowInfo);
                }
            }
            catch { }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// 获取活跃窗口
    /// </summary>
    public WindowInfo? GetActiveWindow()
    {
        var hWnd = GetForegroundWindow();
        return GetWindowInfo(hWnd);
    }

    /// <summary>
    /// 按标题获取窗口
    /// </summary>
    public WindowInfo? GetWindowByTitle(string title)
    {
        var windows = ListAllWindows();
        return windows.FirstOrDefault(w =>
            w.Title.Equals(title, StringComparison.OrdinalIgnoreCase) ||
            w.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 激活窗口
    /// </summary>
    public bool ActivateWindow(string hWndOrTitle)
    {
        try
        {
            IntPtr hWnd = GetHwndFromString(hWndOrTitle);
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
                return false;

            // 如果窗口处于最小化状态，先恢复再激活
            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }

            return SetForegroundWindow(hWnd);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 最小化窗口
    /// </summary>
    public bool MinimizeWindow(string hWndOrTitle)
    {
        return ControlWindow(hWndOrTitle, SW_MINIMIZE);
    }

    /// <summary>
    /// 最大化窗口
    /// </summary>
    public bool MaximizeWindow(string hWndOrTitle)
    {
        return ControlWindow(hWndOrTitle, SW_MAXIMIZE);
    }

    /// <summary>
    /// 恢复窗口
    /// </summary>
    public bool RestoreWindow(string hWndOrTitle)
    {
        return ControlWindow(hWndOrTitle, SW_RESTORE);
    }

    /// <summary>
    /// 关闭窗口
    /// </summary>
    public bool CloseWindow(string hWndOrTitle)
    {
        try
        {
            IntPtr hWnd = GetHwndFromString(hWndOrTitle);
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
                return false;

            // 发送关闭消息（比 ShowWindow 更温和）
            SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 移动和调整窗口大小
    /// </summary>
    public bool MoveWindowPosition(string hWndOrTitle, int x, int y, int width, int height)
    {
        try
        {
            IntPtr hWnd = GetHwndFromString(hWndOrTitle);
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
                return false;

            return MoveWindow(hWnd, x, y, width, height, true);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取窗口信息
    /// </summary>
    private WindowInfo? GetWindowInfo(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return null;

        try
        {
            // 获取标题
            var titleLength = GetWindowTextLength(hWnd);
            if (titleLength == 0)
                return null;

            var titleBuilder = new StringBuilder(titleLength + 1);
            GetWindowText(hWnd, titleBuilder, titleLength + 1);
            var title = titleBuilder.ToString();

            // 跳过空标题和系统窗口
            if (string.IsNullOrWhiteSpace(title))
                return null;

            // 获取位置大小
            GetWindowRect(hWnd, out var rect);

            // 获取进程信息
            GetWindowThreadProcessId(hWnd, out var processId);
            var process = Process.GetProcessById((int)processId);

            // 获取状态
            var isVisible = IsWindowVisible(hWnd);
            var isMinimized = IsIconic(hWnd);
            var isMaximized = IsZoomed(hWnd);
            var isActive = hWnd == GetForegroundWindow();

            return new WindowInfo
            {
                Hwnd = $"0x{hWnd.ToInt64():x}",
                Title = title,
                ProcessName = process.ProcessName,
                ProcessId = (int)processId,
                Rect = new WindowRect
                {
                    X = rect.Left,
                    Y = rect.Top,
                    Width = rect.Right - rect.Left,
                    Height = rect.Bottom - rect.Top
                },
                IsVisible = isVisible,
                IsMinimized = isMinimized,
                IsMaximized = isMaximized,
                IsActive = isActive
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 控制窗口（最小化/最大化/恢复）
    /// </summary>
    private bool ControlWindow(string hWndOrTitle, int command)
    {
        try
        {
            IntPtr hWnd = GetHwndFromString(hWndOrTitle);
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
                return false;

            // ShowWindow 的返回值表示窗口之前是否可见，不代表操作是否成功
            ShowWindow(hWnd, command);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从字符串获取 HWND
    /// </summary>
    private IntPtr GetHwndFromString(string hWndOrTitle)
    {
        if (hWndOrTitle.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ParseHexHwnd(hWndOrTitle);
        }

        var window = GetWindowByTitle(hWndOrTitle);
        if (window == null)
            return IntPtr.Zero;

        return ParseHexHwnd(window.Hwnd);
    }

    /// <summary>
    /// 将十六进制字符串（可带 0x 前缀）解析为 IntPtr
    /// </summary>
    private static IntPtr ParseHexHwnd(string hex)
    {
        var cleaned = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? hex[2..]
            : hex;
        return new IntPtr(Convert.ToInt64(cleaned, 16));
    }
}
