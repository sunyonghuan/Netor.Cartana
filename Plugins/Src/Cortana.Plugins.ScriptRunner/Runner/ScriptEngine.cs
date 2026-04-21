using System.Diagnostics;
using System.Text;

using Cortana.Plugins.ScriptRunner.Globals;
using Cortana.Plugins.ScriptRunner.Tools;

using Dotnet.Script.DependencyModel.NuGet;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Cortana.Plugins.ScriptRunner.Runner;

/// <summary>
/// Roslyn CSharpScript 的封装。M1 版：支持 globals、运行字符串/文件、单表达式求值、语法检查、格式化。
/// </summary>
internal sealed class ScriptEngine
{
    private readonly ScriptGlobals _globals;
    private readonly ScriptOptions _defaultOptions;
    private readonly NuGetResolver? _nugetResolver;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ScriptState<object?>> _sessions
        = new(StringComparer.Ordinal);

    public ScriptEngine(ScriptGlobals globals, NuGetResolver? nugetResolver = null)
    {
        _globals = globals;
        _nugetResolver = nugetResolver;
        _defaultOptions = ScriptOptions.Default
            .WithReferences(
                typeof(object).Assembly,
                typeof(System.Linq.Enumerable).Assembly,
                typeof(System.Collections.Generic.List<>).Assembly,
                typeof(System.IO.File).Assembly,
                typeof(System.Text.StringBuilder).Assembly,
                typeof(System.Text.RegularExpressions.Regex).Assembly,
                typeof(System.Threading.Tasks.Task).Assembly,
                typeof(System.Console).Assembly,
                typeof(System.Net.Http.HttpClient).Assembly,
                typeof(System.Text.Json.JsonSerializer).Assembly,
                typeof(Microsoft.Extensions.Logging.ILogger).Assembly,
                typeof(ScriptGlobals).Assembly)
            .WithImports(
                "System",
                "System.IO",
                "System.Linq",
                "System.Text",
                "System.Text.RegularExpressions",
                "System.Threading",
                "System.Threading.Tasks",
                "System.Collections.Generic",
                "Microsoft.Extensions.Logging");
    }

    public async Task<RunResult> RunStringAsync(string code, int? timeoutMs, CancellationToken outerToken)
    {
        if (code.Length > Limits.MaxCodeChars)
            throw new ArgumentException(
                $"code 长度 {code.Length} 超过上限 {Limits.MaxCodeChars}。大脚本请用 sys_csx_run_file（写到磁盘）。");
        var opts = await PrepareOptionsAsync(code, _defaultOptions, outerToken).ConfigureAwait(false);
        return await ExecuteAsync(code, opts, timeoutMs, outerToken).ConfigureAwait(false);
    }

    /// <summary>若代码包含 <c>#r "nuget:..."</c> 且已启用 NuGet 解析，则还原并返回追加了引用的 ScriptOptions。</summary>
    private async Task<ScriptOptions> PrepareOptionsAsync(string code, ScriptOptions options, CancellationToken ct)
    {
        if (_nugetResolver is null || !NuGetResolver.ContainsNuGetDirective(code))
            return options;
        var refs = await _nugetResolver.ResolveAsync(code, ct).ConfigureAwait(false);
        // NuGetMetadataReferenceResolver 做装饰：遇到 nuget: 前缀返回占位引用避免 Roslyn 报错；
        // 其它 #r "xxx.dll" 仍走默认 ScriptMetadataResolver。
        return options
            .WithMetadataResolver(new NuGetMetadataReferenceResolver(ScriptMetadataResolver.Default))
            .AddReferences(refs);
    }

    public async Task<RunResult> RunFileAsync(string path, int? timeoutMs, CancellationToken outerToken)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"脚本文件不存在：{path}", path);

        var code = await File.ReadAllTextAsync(path, outerToken).ConfigureAwait(false);
        var baseDir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory();
        var fileOpts = _defaultOptions
            .WithFilePath(path)
            .WithSourceResolver(new SourceFileResolver([], baseDir));
        fileOpts = await PrepareOptionsAsync(code, fileOpts, outerToken).ConfigureAwait(false);
        return await ExecuteAsync(code, fileOpts, timeoutMs, outerToken).ConfigureAwait(false);
    }

    public async Task<RunResult> EvaluateAsync(string expression, int? timeoutMs, CancellationToken outerToken)
    {
        if (expression.Length > Limits.MaxCodeChars)
            throw new ArgumentException(
                $"expr 长度 {expression.Length} 超过上限 {Limits.MaxCodeChars}。");

        var timeout = TimeSpan.FromMilliseconds(timeoutMs is > 0 ? timeoutMs.Value : 30_000);
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(outerToken, timeoutCts.Token);

        if (_globals.Host is ScriptHost sh) sh.Cancellation = linked.Token;

        var evalOpts = await PrepareOptionsAsync(expression, _defaultOptions, linked.Token).ConfigureAwait(false);

        var sw = Stopwatch.StartNew();
        var evalTask = Task.Run(() => CSharpScript.EvaluateAsync<object?>(
            expression,
            evalOpts,
            globals: _globals,
            globalsType: typeof(ScriptGlobals),
            cancellationToken: linked.Token));

        var completed = await Task.WhenAny(evalTask, Task.Delay(timeout, outerToken)).ConfigureAwait(false);
        if (completed != evalTask)
        {
            timeoutCts.Cancel();
            _ = evalTask.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
            throw new OperationCanceledException(
                $"表达式求值超时（{timeout.TotalMilliseconds:F0}ms），后台线程已弃用（可能存在死循环或阻塞）。");
        }

        var value = await evalTask.ConfigureAwait(false);
        sw.Stop();

        return new RunResult
        {
            ReturnValue = Truncate(value?.ToString(), Limits.MaxReturnValueChars),
            ReturnType = value?.GetType().FullName,
            ElapsedMs = sw.ElapsedMilliseconds,
        };
    }

    public CheckResult Check(string code)
    {
        var script = CSharpScript.Create(code, _defaultOptions, globalsType: typeof(ScriptGlobals));
        var diags = script.Compile();
        return new CheckResult
        {
            Diagnostics = diags.Select(ToDiagnosticInfo).ToList(),
        };
    }

    public FormatResult Format(string code)
    {
        // 使用 SyntaxNode.NormalizeWhitespace 做基础格式化（不依赖 Workspaces 包）。
        var parseOpts = new CSharpParseOptions(kind: SourceCodeKind.Script);
        var tree = CSharpSyntaxTree.ParseText(code, parseOpts);
        var root = tree.GetRoot();
        var formatted = root.NormalizeWhitespace().ToFullString();
        return new FormatResult { Formatted = formatted };
    }

    // ---------------- M2: 会话（多次交互共享状态） ----------------

    /// <summary>创建一个空会话，返回 sessionId。</summary>
    public string SessionCreate()
    {
        var id = Guid.NewGuid().ToString("n");
        // 预留占位：首次 Exec 时初始化 ScriptState（避免空脚本）。
        _sessions[id] = null!;
        return id;
    }

    /// <summary>在会话里执行一段代码，状态会延续到下次调用。</summary>
    public async Task<RunResult> SessionExecAsync(string sessionId, string code, int? timeoutMs, CancellationToken outerToken)
    {
        if (code.Length > Limits.MaxCodeChars)
            throw new ArgumentException(
                $"code 长度 {code.Length} 超过上限 {Limits.MaxCodeChars}。大脚本请用 sys_csx_run_file。");
        if (!_sessions.ContainsKey(sessionId))
            throw new InvalidOperationException($"会话不存在：{sessionId}");

        var timeout = TimeSpan.FromMilliseconds(timeoutMs is > 0 ? timeoutMs.Value : 30_000);
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(outerToken, timeoutCts.Token);
        if (_globals.Host is ScriptHost sh) sh.Cancellation = linked.Token;

        var stdoutCapture = new StringBuilder();
        var stderrCapture = new StringBuilder();
        var stdoutWriter = new BoundedStringWriter(stdoutCapture, Limits.MaxStdoutChars);
        var stderrWriter = new BoundedStringWriter(stderrCapture, Limits.MaxStderrChars);
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        bool leaked = false;
        var sw = Stopwatch.StartNew();
        try
        {
            Console.SetOut(stdoutWriter);
            Console.SetError(stderrWriter);

            var sessionOpts = await PrepareOptionsAsync(code, _defaultOptions, linked.Token).ConfigureAwait(false);
            var prev = _sessions[sessionId];
            Task<ScriptState<object?>> execTask = prev is null
                ? Task.Run(() => CSharpScript.RunAsync<object?>(
                    code, sessionOpts, globals: _globals, globalsType: typeof(ScriptGlobals),
                    cancellationToken: linked.Token))
                : Task.Run(() => prev.ContinueWithAsync<object?>(code, sessionOpts, cancellationToken: linked.Token));

            var completed = await Task.WhenAny(execTask, Task.Delay(timeout, outerToken)).ConfigureAwait(false);
            if (completed != execTask)
            {
                leaked = true;
                timeoutCts.Cancel();
                _ = execTask.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
                throw new OperationCanceledException(
                    $"会话执行超时（{timeout.TotalMilliseconds:F0}ms），本次代码未合并入会话状态。");
            }

            var newState = await execTask.ConfigureAwait(false);
            _sessions[sessionId] = newState;
            sw.Stop();
            return new RunResult
            {
                ReturnValue = Truncate(newState.ReturnValue?.ToString(), Limits.MaxReturnValueChars),
                ReturnType = newState.ReturnValue?.GetType().FullName,
                Stdout = FinalizeCapture(stdoutCapture, stdoutWriter.Truncated),
                Stderr = FinalizeCapture(stderrCapture, stderrWriter.Truncated),
                ElapsedMs = sw.ElapsedMilliseconds,
            };
        }
        finally
        {
            if (leaked)
            {
                Console.SetOut(TextWriter.Null);
                Console.SetError(TextWriter.Null);
            }
            else
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
            }
        }
    }

    /// <summary>重置会话：清空已累积的状态，sessionId 保留可复用。</summary>
    public bool SessionReset(string sessionId)
    {
        if (!_sessions.ContainsKey(sessionId)) return false;
        _sessions[sessionId] = null!;
        return true;
    }

    /// <summary>关闭并移除会话。</summary>
    public bool SessionClose(string sessionId) => _sessions.TryRemove(sessionId, out _);


    private async Task<RunResult> ExecuteAsync(
        string code,
        ScriptOptions options,
        int? timeoutMs,
        CancellationToken outerToken)
    {
        var timeout = TimeSpan.FromMilliseconds(timeoutMs is > 0 ? timeoutMs.Value : 30_000);
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(outerToken, timeoutCts.Token);

        if (_globals.Host is ScriptHost sh) sh.Cancellation = linked.Token;

        var stdoutCapture = new StringBuilder();
        var stderrCapture = new StringBuilder();
        var stdoutWriter = new BoundedStringWriter(stdoutCapture, Limits.MaxStdoutChars);
        var stderrWriter = new BoundedStringWriter(stderrCapture, Limits.MaxStderrChars);
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        bool leaked = false;
        var sw = Stopwatch.StartNew();
        try
        {
            Console.SetOut(stdoutWriter);
            Console.SetError(stderrWriter);

            var scriptTask = Task.Run(() => CSharpScript.RunAsync(
                code,
                options,
                globals: _globals,
                globalsType: typeof(ScriptGlobals),
                cancellationToken: linked.Token));

            var completed = await Task.WhenAny(scriptTask, Task.Delay(timeout, outerToken)).ConfigureAwait(false);
            if (completed != scriptTask)
            {
                // 硬超时：Roslyn 对 CPU 紧致循环不响应 CancellationToken，
                // 这里放弃等待，让 scriptTask 在后台自生自灭（线程会泄漏直到自然结束）。
                leaked = true;
                timeoutCts.Cancel();
                _ = scriptTask.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
                throw new OperationCanceledException(
                    $"脚本执行超时（{timeout.TotalMilliseconds:F0}ms），后台线程已弃用（可能存在死循环或阻塞）。");
            }

            var state = await scriptTask.ConfigureAwait(false);
            sw.Stop();
            return new RunResult
            {
                ReturnValue = Truncate(state.ReturnValue?.ToString(), Limits.MaxReturnValueChars),
                ReturnType = state.ReturnValue?.GetType().FullName,
                Stdout = FinalizeCapture(stdoutCapture, stdoutWriter.Truncated),
                Stderr = FinalizeCapture(stderrCapture, stderrWriter.Truncated),
                ElapsedMs = sw.ElapsedMilliseconds,
            };
        }
        finally
        {
            if (leaked)
            {
                // 泄漏的脚本仍持有线程；把 Console 重定向到 Null，防止其后续输出污染
                // 真实 stdout/stderr（协议走独立 StreamWriter，不受影响；这里主要防 stderr 日志噪音）。
                Console.SetOut(TextWriter.Null);
                Console.SetError(TextWriter.Null);
            }
            else
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
            }
        }
    }

    private static string? Truncate(string? value, int maxChars)
    {
        if (value is null) return null;
        if (value.Length <= maxChars) return value;
        return value.AsSpan(0, maxChars).ToString() + Limits.TruncatedMark;
    }

    private static string FinalizeCapture(StringBuilder sb, bool truncated)
    {
        if (!truncated) return sb.ToString();
        return sb.ToString() + Limits.TruncatedMark;
    }

    private static DiagnosticInfo ToDiagnosticInfo(Diagnostic d)
    {
        var span = d.Location.GetLineSpan();
        return new DiagnosticInfo
        {
            Severity = d.Severity.ToString(),
            Id = d.Id,
            Line = span.StartLinePosition.Line + 1,
            Column = span.StartLinePosition.Character + 1,
            Message = d.GetMessage(),
        };
    }
}
