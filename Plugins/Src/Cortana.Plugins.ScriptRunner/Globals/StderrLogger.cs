using Microsoft.Extensions.Logging;

namespace Cortana.Plugins.ScriptRunner.Globals;

/// <summary>
/// 把日志写到 stderr 的极简 ILogger。
/// stdout 独占协议 JSON，严禁污染；脚本的 Log.XXX() 自动落到 stderr，
/// 宿主侧 ExternalProcessPluginHostBase 会把 stderr 以 LogWarning 记录。
/// </summary>
internal sealed class StderrLogger : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var message = formatter(state, exception);
        var line = exception is null
            ? $"[{logLevel}] {message}"
            : $"[{logLevel}] {message} :: {exception}";
        Console.Error.WriteLine(line);
    }
}
