using Microsoft.Extensions.Logging;

namespace Cortana.Plugins.ScriptRunner.Globals;

/// <summary>
/// 脚本全局对象。脚本内直接写 Log / Settings / Host 即可访问。
/// 通过 ScriptOptions 注入到 CSharpScript 作为 globals 实参。
/// </summary>
public sealed class ScriptGlobals
{
    public ILogger Log { get; }
    public ISettingsBridge Settings { get; }
    public IScriptHost Host { get; }

    public ScriptGlobals(ILogger log, ISettingsBridge settings, IScriptHost host)
    {
        Log = log;
        Settings = settings;
        Host = host;
    }
}
