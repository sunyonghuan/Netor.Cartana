namespace Cortana.Plugins.ScriptRunner.Globals;

/// <summary>
/// 暴露给脚本的宿主信息（目录、取消令牌）。
/// </summary>
public interface IScriptHost
{
    /// <summary>插件安装目录。</summary>
    string PluginDirectory { get; }

    /// <summary>插件专属数据目录。</summary>
    string DataDirectory { get; }

    /// <summary>工作区目录。</summary>
    string WorkspaceDirectory { get; }

    /// <summary>当前脚本执行的取消令牌（超时 / destroy 时会 cancel）。</summary>
    CancellationToken Cancellation { get; }
}
