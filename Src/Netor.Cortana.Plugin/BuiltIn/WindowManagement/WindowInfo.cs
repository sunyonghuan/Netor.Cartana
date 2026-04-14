namespace Netor.Cortana.Plugin.BuiltIn.WindowManagement;

/// <summary>
/// 窗口矩形信息
/// </summary>
public sealed class WindowRect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// 窗口信息模型
/// </summary>
public sealed class WindowInfo
{
    /// <summary>
    /// 窗口句柄（十六进制字符串）
    /// </summary>
    public string Hwnd { get; set; } = string.Empty;

    /// <summary>
    /// 窗口标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 进程名称
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// 进程 ID
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// 窗口位置和大小
    /// </summary>
    public WindowRect Rect { get; set; } = new();

    /// <summary>
    /// 是否可见
    /// </summary>
    public bool IsVisible { get; set; }

    /// <summary>
    /// 是否最小化
    /// </summary>
    public bool IsMinimized { get; set; }

    /// <summary>
    /// 是否最大化
    /// </summary>
    public bool IsMaximized { get; set; }

    /// <summary>
    /// 是否为活跃窗口
    /// </summary>
    public bool IsActive { get; set; }
}
