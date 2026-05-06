namespace Cortana.Plugins.ApplicationLauncher;

/// <summary>
/// 描述一个可由插件识别和启动的应用程序。
/// </summary>
public sealed class ApplicationInfo
{
    /// <summary>应用显示名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>应用可执行文件路径。</summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>应用文件描述信息。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>应用文件版本。</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>应用分类，例如 IDE、Browser、Development。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>图标路径；当前使用可执行文件路径。</summary>
    public string IconPath { get; set; } = string.Empty;

    /// <summary>应用可执行文件是否存在。</summary>
    public bool IsAvailable { get; set; }

    /// <summary>应用是否允许由插件启动。</summary>
    public bool IsLaunchable { get; set; }
}

/// <summary>
/// 应用启动操作结果。
/// </summary>
public sealed class ApplicationLaunchResult
{
    /// <summary>是否启动成功。</summary>
    public bool Success { get; set; }

    /// <summary>请求启动的应用名称。</summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>启动成功后的进程 ID。</summary>
    public int ProcessId { get; set; }

    /// <summary>启动请求发生的时间。</summary>
    public DateTime LaunchTime { get; set; }

    /// <summary>失败时的错误信息。</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>启动操作耗时，单位毫秒。</summary>
    public long ElapsedMs { get; set; }
}

/// <summary>
/// 使用指定应用打开文件的操作结果。
/// </summary>
public sealed class OpenFileResult
{
    /// <summary>是否打开成功。</summary>
    public bool Success { get; set; }

    /// <summary>用于打开文件的应用名称。</summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>被打开的文件路径。</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>启动成功后的进程 ID。</summary>
    public int ProcessId { get; set; }

    /// <summary>失败时的错误信息。</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>打开操作耗时，单位毫秒。</summary>
    public long ElapsedMs { get; set; }
}
