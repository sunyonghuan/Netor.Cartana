namespace Cortana.Plugins.WindowManagement;

/// <summary>
/// 表示窗口矩形区域的位置和大小。
/// </summary>
public sealed class WindowRect
{
    /// <summary>窗口左上角 X 坐标。</summary>
    public int X { get; set; }

    /// <summary>窗口左上角 Y 坐标。</summary>
    public int Y { get; set; }

    /// <summary>窗口宽度。</summary>
    public int Width { get; set; }

    /// <summary>窗口高度。</summary>
    public int Height { get; set; }
}

/// <summary>
/// 描述一个 Windows 顶层窗口的基础信息。
/// </summary>
public sealed class WindowInfo
{
    /// <summary>窗口句柄，使用十六进制字符串表示。</summary>
    public string Hwnd { get; set; } = string.Empty;

    /// <summary>窗口标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>所属进程名称。</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>所属进程 ID。</summary>
    public int ProcessId { get; set; }

    /// <summary>窗口位置和大小。</summary>
    public WindowRect Rect { get; set; } = new();

    /// <summary>窗口是否可见。</summary>
    public bool IsVisible { get; set; }

    /// <summary>窗口是否处于最小化状态。</summary>
    public bool IsMinimized { get; set; }

    /// <summary>窗口是否处于最大化状态。</summary>
    public bool IsMaximized { get; set; }

    /// <summary>窗口是否为当前前台活跃窗口。</summary>
    public bool IsActive { get; set; }
}
