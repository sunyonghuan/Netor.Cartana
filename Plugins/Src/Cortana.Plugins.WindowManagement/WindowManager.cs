using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ProcessDiag = System.Diagnostics.Process;

namespace Cortana.Plugins.WindowManagement;

/// <summary>
/// Windows 窗口管理器，封装窗口枚举、查询和控制相关 Win32 API。
/// </summary>
public sealed class WindowManager
{
    private const int SW_MINIMIZE = 6;
    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;
    private const uint WM_CLOSE = 0x0010;

    /// <summary>
    /// EnumWindows 使用的窗口枚举回调委托。
    /// </summary>
    /// <param name="hWnd">当前枚举到的窗口句柄。</param>
    /// <param name="lParam">调用方传入的附加参数。</param>
    /// <returns>返回 <see langword="true" /> 继续枚举；返回 <see langword="false" /> 停止枚举。</returns>
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

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

    /// <summary>
    /// Win32 RECT 结构，表示窗口边界。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        /// <summary>左边界坐标。</summary>
        public int Left;
        /// <summary>上边界坐标。</summary>
        public int Top;
        /// <summary>右边界坐标。</summary>
        public int Right;
        /// <summary>下边界坐标。</summary>
        public int Bottom;
    }

    /// <summary>
    /// 枚举系统中所有带标题的顶层窗口。
    /// </summary>
    /// <returns>窗口信息列表。</returns>
    public List<WindowInfo> ListAllWindows()
    {
        var windows = new List<WindowInfo>();

        EnumWindows((hWnd, _) =>
        {
            try
            {
                var windowInfo = GetWindowInfo(hWnd);
                if (windowInfo is not null)
                {
                    windows.Add(windowInfo);
                }
            }
            catch
            {
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// 获取当前前台活跃窗口的信息。
    /// </summary>
    /// <returns>活跃窗口信息；获取失败时返回 <see langword="null" />。</returns>
    public WindowInfo? GetActiveWindow()
    {
        var hWnd = GetForegroundWindow();
        return GetWindowInfo(hWnd);
    }

    /// <summary>
    /// 根据窗口标题查找窗口，支持不区分大小写的完整匹配和包含匹配。
    /// </summary>
    /// <param name="title">窗口标题或标题片段。</param>
    /// <returns>匹配到的第一个窗口；未匹配时返回 <see langword="null" />。</returns>
    public WindowInfo? GetWindowByTitle(string title)
    {
        var windows = ListAllWindows();
        return windows.FirstOrDefault(w =>
            w.Title.Equals(title, StringComparison.OrdinalIgnoreCase) ||
            w.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
    }

            /// <summary>
            /// 激活指定窗口；如果窗口已最小化，会先恢复窗口。
            /// </summary>
            /// <param name="hWndOrTitle">窗口句柄十六进制字符串或窗口标题。</param>
            /// <returns>是否成功发起激活操作。</returns>
    public bool ActivateWindow(string hWndOrTitle)
    {
        try
        {
            var hWnd = GetHwndFromString(hWndOrTitle);
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
            {
                return false;
            }

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
    /// 最小化指定窗口。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄十六进制字符串或窗口标题。</param>
    /// <returns>是否成功发起最小化操作。</returns>
    public bool MinimizeWindow(string hWndOrTitle)
    {
        return ControlWindow(hWndOrTitle, SW_MINIMIZE);
    }

    /// <summary>
    /// 最大化指定窗口。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄十六进制字符串或窗口标题。</param>
    /// <returns>是否成功发起最大化操作。</returns>
    public bool MaximizeWindow(string hWndOrTitle)
    {
        return ControlWindow(hWndOrTitle, SW_MAXIMIZE);
    }

    /// <summary>
    /// 将指定窗口恢复到普通大小。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄十六进制字符串或窗口标题。</param>
    /// <returns>是否成功发起恢复操作。</returns>
    public bool RestoreWindow(string hWndOrTitle)
    {
        return ControlWindow(hWndOrTitle, SW_RESTORE);
    }

    /// <summary>
    /// 向指定窗口发送关闭消息。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄十六进制字符串或窗口标题。</param>
    /// <returns>是否成功发送关闭消息。</returns>
    public bool CloseWindow(string hWndOrTitle)
    {
        try
        {
            var hWnd = GetHwndFromString(hWndOrTitle);
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
            {
                return false;
            }

            SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 移动窗口并调整窗口大小。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄十六进制字符串或窗口标题。</param>
    /// <param name="x">目标 X 坐标。</param>
    /// <param name="y">目标 Y 坐标。</param>
    /// <param name="width">目标宽度。</param>
    /// <param name="height">目标高度。</param>
    /// <returns>是否成功发起移动操作。</returns>
    public bool MoveWindowPosition(string hWndOrTitle, int x, int y, int width, int height)
    {
        try
        {
            var hWnd = GetHwndFromString(hWndOrTitle);
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
            {
                return false;
            }

            return MoveWindow(hWnd, x, y, width, height, true);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 根据窗口句柄读取窗口标题、进程、位置和状态信息。
    /// </summary>
    /// <param name="hWnd">窗口句柄。</param>
    /// <returns>窗口信息；窗口无效、无标题或读取失败时返回 <see langword="null" />。</returns>
    private WindowInfo? GetWindowInfo(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var titleLength = GetWindowTextLength(hWnd);
            if (titleLength == 0)
            {
                return null;
            }

            var titleBuilder = new StringBuilder(titleLength + 1);
            GetWindowText(hWnd, titleBuilder, titleLength + 1);
            var title = titleBuilder.ToString();

            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            GetWindowRect(hWnd, out var rect);
            GetWindowThreadProcessId(hWnd, out var processId);
            var process = ProcessDiag.GetProcessById((int)processId);

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
                IsVisible = IsWindowVisible(hWnd),
                IsMinimized = IsIconic(hWnd),
                IsMaximized = IsZoomed(hWnd),
                IsActive = hWnd == GetForegroundWindow()
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 通过 ShowWindow 控制窗口状态。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄十六进制字符串或窗口标题。</param>
    /// <param name="command">ShowWindow 命令。</param>
    /// <returns>是否成功发起控制操作。</returns>
    private bool ControlWindow(string hWndOrTitle, int command)
    {
        try
        {
            var hWnd = GetHwndFromString(hWndOrTitle);
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
            {
                return false;
            }

            ShowWindow(hWnd, command);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 将窗口句柄字符串或窗口标题解析为窗口句柄。
    /// </summary>
    /// <param name="hWndOrTitle">窗口句柄十六进制字符串或窗口标题。</param>
    /// <returns>窗口句柄；解析失败时返回 <see cref="IntPtr.Zero" />。</returns>
    private IntPtr GetHwndFromString(string hWndOrTitle)
    {
        if (hWndOrTitle.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ParseHexHwnd(hWndOrTitle);
        }

        var window = GetWindowByTitle(hWndOrTitle);
        return window is null ? IntPtr.Zero : ParseHexHwnd(window.Hwnd);
    }

    /// <summary>
    /// 将十六进制窗口句柄字符串解析为 <see cref="IntPtr" />。
    /// </summary>
    /// <param name="hex">可带 0x 前缀的十六进制句柄字符串。</param>
    /// <returns>解析后的窗口句柄。</returns>
    private static IntPtr ParseHexHwnd(string hex)
    {
        var cleaned = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? hex[2..]
            : hex;

        return new IntPtr(Convert.ToInt64(cleaned, 16));
    }
}
