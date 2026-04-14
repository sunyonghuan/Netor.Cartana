namespace Netor.Cortana.Plugin.BuiltIn.ApplicationLauncher;

/// <summary>
/// 应用程序的基本信息
/// </summary>
public class ApplicationInfo
{
    /// <summary>
    /// 应用名称（用户友好）
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 应用执行路径
    /// </summary>
    public string ExecutablePath { get; set; } = "";

    /// <summary>
    /// 应用描述
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 应用版本
    /// </summary>
    public string Version { get; set; } = "";

    /// <summary>
    /// 应用分类（IDE、浏览器、通讯、开发工具等）
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>
    /// 图标路径
    /// </summary>
    public string IconPath { get; set; } = "";

    /// <summary>
    /// 是否可用（文件是否存在）
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// 是否允许启动
    /// </summary>
    public bool IsLaunchable { get; set; }
}

/// <summary>
/// 应用启动结果
/// </summary>
public class ApplicationLaunchResult
{
    /// <summary>
    /// 是否启动成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 应用名称
    /// </summary>
    public string ApplicationName { get; set; } = "";

    /// <summary>
    /// 启动的进程ID
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// 启动时间
    /// </summary>
    public DateTime LaunchTime { get; set; }

    /// <summary>
    /// 错误信息（如果启动失败）
    /// </summary>
    public string ErrorMessage { get; set; } = "";

    /// <summary>
    /// 启动耗时（毫秒）
    /// </summary>
    public long ElapsedMs { get; set; }
}

/// <summary>
/// 使用指定应用打开文件的结果
/// </summary>
public class OpenFileResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 应用名称
    /// </summary>
    public string ApplicationName { get; set; } = "";

    /// <summary>
    /// 文件路径
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// 启动的进程ID
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string ErrorMessage { get; set; } = "";

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long ElapsedMs { get; set; }
}
