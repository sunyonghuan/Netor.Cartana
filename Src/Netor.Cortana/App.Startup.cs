using System.Reflection;

namespace Netor.Cortana;

internal partial class App
{
    /// <summary>
    /// 获取当前应用程序实例。
    /// </summary>
    internal static WinFormedgeApp FormedgeApp => WinFormedgeApp.Current;

    internal static Formedge? MainWindow { get; private set; }

    /// <summary>
    /// 应用程序图标（从嵌入资源加载）。
    /// </summary>
    internal static Icon AppIcon { get; } = LoadAppIcon();

    private static Icon LoadAppIcon()
    {
        var stream = typeof(App).Assembly.GetManifestResourceStream("Netor.Cortana.logo.200.ico");

        return stream is not null
            ? new Icon(stream)
            : SystemIcons.Application;
    }

    /// <summary>
    /// 应用程序配置选项。
    /// </summary>
    private static AppOptions Options { get; set; } = new AppOptions();

    /// <summary>
    /// 应用程序域名。
    /// </summary>
    internal static string Domain => Options.Domain;

    /// <summary>
    /// 应用程序通信协议（如 http、https）。
    /// </summary>
    internal static string Scheme => Options.Scheme;

    /// <summary>
    /// 应用程序版本号。
    /// </summary>
    internal static string Version => Options.Version;

    /// <summary>
    /// 应用程序根 URI，由协议和域名拼接而成。
    /// </summary>
    internal static string Uri => $"{Scheme}://{Domain}";

    /// <summary>
    /// 获取应用程序服务集合。
    /// </summary>
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    internal static IServiceProvider Services { get; private set; }
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

    /// <summary>
    /// 工作区路径。
    /// </summary>
    internal static string WorkspaceDirectory { get; private set; } = UserDataDirectory;

    /// <summary>
    /// 用户数据路径。
    /// 使用 exe 实际物理路径的目录，确保单文件发布模式下也能正确定位
    /// （Directory.GetCurrentDirectory 取决于启动方式，不可靠）。
    /// </summary>
    internal static string UserDataDirectory =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? Directory.GetCurrentDirectory();

    /// <summary>
    /// 插件目录路径。优先使用 AppOptions.PluginPath，若未配置则使用默认路径。
    /// </summary>
    internal static string PluginDirectory => WorkspacePluginsDirectory;

    /// <summary>
    /// 工作区技能目录路径。
    /// </summary>
    internal static string WorkspaceSkillsDirectory => Path.Combine(WorkspaceDirectory, ".cortana", "skills");

    /// <summary>
    /// 工作区插件目录路径。
    /// </summary>
    internal static string WorkspacePluginsDirectory => Path.Combine(WorkspaceDirectory, ".cortana", "plugins");

    /// <summary>
    /// 用户数据技能目录路径。
    /// </summary>
    internal static string UserSkillsDirectory => Path.Combine(UserDataDirectory, "skills");

    /// <summary>
    /// 用户数据插件目录路径。
    /// </summary>
    internal static string UserPluginsDirectory => Path.Combine(UserDataDirectory, "plugins");

    /// <summary>
    /// 配置应用程序服务。
    /// </summary>
    /// <param name="configure"></param>
    internal static void ConfigureServices(Action<IServiceCollection> configure)
    {
        IServiceCollection services = new ServiceCollection();

        configure(services);
        Services = services.BuildServiceProvider();
    }

    /// <summary>
    /// 将相对路径映射为完整的应用程序 URI。
    /// </summary>
    /// <param name="path">相对路径（如 "api/users"）。</param>
    /// <returns>完整的 URI 字符串。</returns>
    internal static string Map(string path) => $"{Uri}/{path}";

    /// <summary>
    /// 配置应用程序选项。
    /// </summary>
    /// <param name="configure">用于配置 <see cref="AppOptions"/> 的委托。</param>
    internal static void Configure(Action<AppOptions> configure)
    => configure(Options);

    /// <summary>
    /// 更改当前工作区目录，并确保相关技能目录已创建。
    /// </summary>
    /// <remarks>如果指定目录下的 .cortana\skills 子目录不存在，则会自动创建该目录。此操作会影响后续依赖于工作区路径的功能。</remarks>
    /// <param name="path">要设置为当前工作区的目录路径。不能为空，且应为有效的文件系统路径。</param>
    internal static void ChangeWorkspaceDirectory(string path)
    {
        WorkspaceDirectory = path;
        var cortanaPath = Path.Combine(App.WorkspaceDirectory, ".cortana");
        if (!Directory.Exists(cortanaPath))
            Directory.CreateDirectory(cortanaPath);
        if (!Directory.Exists(WorkspaceSkillsDirectory))
            Directory.CreateDirectory(WorkspaceSkillsDirectory);
        if (!Directory.Exists(WorkspacePluginsDirectory))
            Directory.CreateDirectory(WorkspacePluginsDirectory);
    }
}

/// <summary>
/// 应用程序配置选项记录。
/// </summary>
internal record AppOptions()
{
    /// <summary>
    /// 应用程序域名。
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// 应用程序通信协议（如 http、https）。
    /// </summary>
    public string Scheme { get; set; } = string.Empty;

    /// <summary>
    /// 应用程序版本号，通过反射从程序集信息中动态读取。
    /// </summary>
    public string Version
    { get; set; }
    = Assembly.GetExecutingAssembly()
          .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
          .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
          .FirstOrDefault()?
          .InformationalVersion ?? "0.0.0";
}