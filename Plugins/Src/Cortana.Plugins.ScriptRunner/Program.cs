using System.Text;
using System.Text.Json;

using Cortana.Plugins.ScriptRunner.Globals;
using Cortana.Plugins.ScriptRunner.Protocol;
using Cortana.Plugins.ScriptRunner.Runner;
using Cortana.Plugins.ScriptRunner.Tools;

namespace Cortana.Plugins.ScriptRunner;

internal static class Program
{
    private static ScriptEngine? _engine;
    private static InitConfig? _initConfig;

    private static async Task<int> Main()
    {
        // 关键：stdout 只能写协议 JSON；我们用独占 StreamWriter 包装。
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))
        {
            AutoFlush = false,
        };
        var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);

        while (await stdin.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            HostResponse response;
            HostRequest? request = null;
            try
            {
                if (line.Length > Runner.Limits.MaxRequestLineBytes)
                {
                    response = HostResponse.Fail(
                        $"请求帧长度 {line.Length} 超过上限 {Runner.Limits.MaxRequestLineBytes}；" +
                        "请将大脚本通过 csx_run_file 执行，将大参数写入文件后传路径。");
                }
                else
                {
                    request = JsonSerializer.Deserialize(line, ProtocolJsonContext.Default.HostRequest);
                    if (request is null)
                    {
                        response = HostResponse.Fail("请求反序列化为 null");
                    }
                    else
                    {
                        response = await DispatchAsync(request).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                response = HostResponse.Fail($"请求处理异常：{ex.GetType().Name}: {ex.Message}");
            }

            var json = JsonSerializer.Serialize(response, ProtocolJsonContext.Default.HostResponse);
            await stdout.WriteLineAsync(json).ConfigureAwait(false);
            await stdout.FlushAsync().ConfigureAwait(false);

            if (request?.Method == "destroy") break;
        }

        return 0;
    }

    private static async Task<HostResponse> DispatchAsync(HostRequest request)
    {
        return request.Method switch
        {
            "get_info" => HandleGetInfo(),
            "init" => HandleInit(request.Args),
            "invoke" => await HandleInvokeAsync(request.ToolName, request.Args).ConfigureAwait(false),
            "destroy" => HostResponse.Ok(),
            _ => HostResponse.Fail($"未知 method: {request.Method}"),
        };
    }

    private static bool? _sdkAvailableCache;

    /// <summary>检查 host 是否安装了 .NET SDK（`dotnet --list-sdks` 有非空输出），决定是否启用 NuGet 还原。</summary>
    private static bool ProbeDotnetSdk()
    {
        if (_sdkAvailableCache.HasValue) return _sdkAvailableCache.Value;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("dotnet", "--list-sdks")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null) { _sdkAvailableCache = false; return false; }
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            var ok = p.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout);
            _sdkAvailableCache = ok;
            return ok;
        }
        catch
        {
            _sdkAvailableCache = false;
            return false;
        }
    }

    private static HostResponse HandleGetInfo()
    {
        var nuget = ProbeDotnetSdk();
        var nugetLine = nuget
            ? "NuGet：脚本首行或靠前位置可写 #r \"nuget:Humanizer, 2.14.1\" 引入包；首次调用会 dotnet restore 下载，结果会在 dataDir 缓存。"
            : "NuGet：当前宿主未检测到 .NET SDK（仅有 Runtime），#r \"nuget:...\" 暂不可用；如需使用请安装 .NET SDK 后重启插件。";
        var info = new PluginInfo
        {
            Id = "csx_script",
            Name = "C# Script Runner",
            Version = "1.0.0",
            Description = "在 .NET Runtime 上执行 C# 脚本（CSX）。基于 Roslyn Scripting。",
            Tags = nuget ? ["C#", "脚本", "CSX", "Roslyn", "nuget"] : ["C#", "脚本", "CSX", "Roslyn"],
            Instructions =
                "本插件提供 C# 脚本（CSX）执行能力。\n" +
                "- csx_run_str：执行一段源码字符串，返回值与 Console 输出一并返回。\n" +
                "- csx_run_file：执行 .csx 或 .cs 脚本文件，支持 #load 相对路径。\n" +
                "- csx_eval：单表达式求值，不允许语句。\n" +
                "- csx_check：只做语法/语义检查并返回诊断，不执行。\n" +
                "- csx_format：基础格式化（SyntaxNode.NormalizeWhitespace）。\n" +
                "- csx_session_create/exec/reset/close：交互式会话，可跨多次调用累积变量与 using。\n" +
                "脚本可直接使用 Log / Settings / Host 三个全局对象。\n" +
                nugetLine,
            Tools =
            [
                new ToolInfo
                {
                    Name = "csx_run_str",
                    Description = "执行一段 C# 脚本源码字符串（CSX），返回执行结果、标准输出和耗时。",
                    Parameters =
                    [
                        new ToolParameter { Name = "code", Type = "string", Description = "要执行的 C# 脚本源码。支持 top-level 语句与 async/await。", Required = true },
                        new ToolParameter { Name = "timeoutMs", Type = "integer", Description = "执行超时毫秒数，默认 30000。", Required = false },
                    ],
                },
                new ToolInfo
                {
                    Name = "csx_run_file",
                    Description = "执行指定路径的 C# 脚本文件（.csx 或 .cs 均可，按 CSX 语义执行；支持 #load 相对路径）。",
                    Parameters =
                    [
                        new ToolParameter { Name = "path", Type = "string", Description = "脚本文件绝对或相对路径，扩展名可为 .csx 或 .cs。", Required = true },
                        new ToolParameter { Name = "timeoutMs", Type = "integer", Description = "执行超时毫秒数，默认 30000。", Required = false },
                    ],
                },
                new ToolInfo
                {
                    Name = "csx_eval",
                    Description = "对单个 C# 表达式求值并返回其字符串形式与类型。",
                    Parameters =
                    [
                        new ToolParameter { Name = "expr", Type = "string", Description = "单个 C# 表达式，例如 DateTime.Now 或 1+2。", Required = true },
                        new ToolParameter { Name = "timeoutMs", Type = "integer", Description = "执行超时毫秒数，默认 30000。", Required = false },
                    ],
                },
                new ToolInfo
                {
                    Name = "csx_check",
                    Description = "对一段 C# 脚本源码进行语法与语义检查，返回诊断列表，不执行。",
                    Parameters =
                    [
                        new ToolParameter { Name = "code", Type = "string", Description = "待检查的 C# 源码。", Required = true },
                    ],
                },
                new ToolInfo
                {
                    Name = "csx_format",
                    Description = "基础格式化一段 C# 脚本源码（Roslyn SyntaxNode.NormalizeWhitespace）。",
                    Parameters =
                    [
                        new ToolParameter { Name = "code", Type = "string", Description = "待格式化的 C# 源码。", Required = true },
                    ],
                },
                new ToolInfo
                {
                    Name = "csx_session_create",
                    Description = "创建一个交互式 C# 会话，返回 sessionId。后续通过 csx_session_exec 在同一会话里累积状态（变量、using、函数）。",
                    Parameters = [],
                },
                new ToolInfo
                {
                    Name = "csx_session_exec",
                    Description = "在指定会话里执行一段 C# 代码。变量、using、局部函数会延续到下次调用。",
                    Parameters =
                    [
                        new ToolParameter { Name = "sessionId", Type = "string", Description = "由 csx_session_create 返回的会话 ID。", Required = true },
                        new ToolParameter { Name = "code", Type = "string", Description = "要执行的 C# 代码片段。", Required = true },
                        new ToolParameter { Name = "timeoutMs", Type = "integer", Description = "执行超时毫秒数，默认 30000。", Required = false },
                    ],
                },
                new ToolInfo
                {
                    Name = "csx_session_reset",
                    Description = "清空会话已累积的状态（变量、using），但保留 sessionId 供继续使用。",
                    Parameters =
                    [
                        new ToolParameter { Name = "sessionId", Type = "string", Description = "会话 ID。", Required = true },
                    ],
                },
                new ToolInfo
                {
                    Name = "csx_session_close",
                    Description = "关闭并移除会话。后续对该 sessionId 的调用会失败。",
                    Parameters =
                    [
                        new ToolParameter { Name = "sessionId", Type = "string", Description = "会话 ID。", Required = true },
                    ],
                },
            ],
        };

        var json = JsonSerializer.Serialize(info, ProtocolJsonContext.Default.PluginInfo);
        return HostResponse.Ok(json);
    }

    private static HostResponse HandleInit(string? argsJson)
    {
        if (!string.IsNullOrWhiteSpace(argsJson))
        {
            try
            {
                _initConfig = JsonSerializer.Deserialize(argsJson, ProtocolJsonContext.Default.InitConfig);
            }
            catch (JsonException ex)
            {
                return HostResponse.Fail($"init 配置解析失败: {ex.Message}");
            }
        }

        var dataDir = _initConfig?.DataDirectory ?? Path.Combine(AppContext.BaseDirectory, "data");
        var pluginDir = _initConfig?.PluginDirectory ?? AppContext.BaseDirectory;
        var workspaceDir = _initConfig?.WorkspaceDirectory ?? Directory.GetCurrentDirectory();

        var host = new ScriptHost
        {
            PluginDirectory = pluginDir,
            DataDirectory = dataDir,
            WorkspaceDirectory = workspaceDir,
        };
        var settings = new JsonFileSettingsBridge(dataDir);
        var log = new StderrLogger();
        var globals = new ScriptGlobals(log, settings, host);

        // NuGet 缓存放到 dataDir 下，避免污染宿主 workspace；dotnet-script 内部会 spawn `dotnet restore`，
        // 要求宿主机装有 .NET SDK（非仅 Runtime）。仅 Runtime 环境下 resolver 传 null，脚本中出现 #r nuget:
        // 指令时会在编译阶段正常报错，而不是在子进程启动时抛 Win32Exception。
        NuGetResolver? nugetResolver = null;
        if (ProbeDotnetSdk())
        {
            var nugetCache = Path.Combine(dataDir, "nuget-script-cache");
            nugetResolver = new NuGetResolver(nugetCache);
        }

        _engine = new ScriptEngine(globals, nugetResolver);
        return HostResponse.Ok();
    }

    private static async Task<HostResponse> HandleInvokeAsync(string? toolName, string? argsJson)
    {
        if (_engine is null)
            return HostResponse.Fail("插件尚未初始化（请先调用 init）");

        try
        {
            return toolName switch
            {
                "csx_run_str" => await RunStrAsync(argsJson).ConfigureAwait(false),
                "csx_run_file" => await RunFileAsync(argsJson).ConfigureAwait(false),
                "csx_eval" => await EvalAsync(argsJson).ConfigureAwait(false),
                "csx_check" => CheckTool(argsJson),
                "csx_format" => FormatTool(argsJson),
                "csx_session_create" => SessionCreateTool(),
                "csx_session_exec" => await SessionExecAsync(argsJson).ConfigureAwait(false),
                "csx_session_reset" => SessionResetTool(argsJson),
                "csx_session_close" => SessionCloseTool(argsJson),
                _ => HostResponse.Fail($"未知工具: {toolName}"),
            };
        }
        catch (Microsoft.CodeAnalysis.Scripting.CompilationErrorException ex)
        {
            return HostResponse.Fail($"编译错误: {string.Join("; ", ex.Diagnostics)}");
        }
        catch (OperationCanceledException)
        {
            return HostResponse.Fail("脚本执行超时");
        }
        catch (FileNotFoundException ex)
        {
            return HostResponse.Fail($"文件未找到: {ex.Message}");
        }
        catch (Exception ex)
        {
            return HostResponse.Fail($"脚本执行异常: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<HostResponse> RunStrAsync(string? argsJson)
    {
        var args = ParseArgs(argsJson, ProtocolJsonContext.Default.RunStrArgs);
        if (args is null || string.IsNullOrEmpty(args.Code))
            return HostResponse.Fail("code 参数不能为空");
        var result = await _engine!.RunStringAsync(args.Code, args.TimeoutMs, CancellationToken.None).ConfigureAwait(false);
        return HostResponse.Ok(JsonSerializer.Serialize(result, ProtocolJsonContext.Default.RunResult));
    }

    private static async Task<HostResponse> RunFileAsync(string? argsJson)
    {
        var args = ParseArgs(argsJson, ProtocolJsonContext.Default.RunFileArgs);
        if (args is null || string.IsNullOrEmpty(args.Path))
            return HostResponse.Fail("path 参数不能为空");
        var result = await _engine!.RunFileAsync(args.Path, args.TimeoutMs, CancellationToken.None).ConfigureAwait(false);
        return HostResponse.Ok(JsonSerializer.Serialize(result, ProtocolJsonContext.Default.RunResult));
    }

    private static async Task<HostResponse> EvalAsync(string? argsJson)
    {
        var args = ParseArgs(argsJson, ProtocolJsonContext.Default.EvalArgs);
        if (args is null || string.IsNullOrEmpty(args.Expression))
            return HostResponse.Fail("expr 参数不能为空");
        var result = await _engine!.EvaluateAsync(args.Expression, args.TimeoutMs, CancellationToken.None).ConfigureAwait(false);
        return HostResponse.Ok(JsonSerializer.Serialize(result, ProtocolJsonContext.Default.RunResult));
    }

    private static HostResponse CheckTool(string? argsJson)
    {
        var args = ParseArgs(argsJson, ProtocolJsonContext.Default.CodeArgs);
        if (args is null || string.IsNullOrEmpty(args.Code))
            return HostResponse.Fail("code 参数不能为空");
        var result = _engine!.Check(args.Code);
        return HostResponse.Ok(JsonSerializer.Serialize(result, ProtocolJsonContext.Default.CheckResult));
    }

    private static HostResponse FormatTool(string? argsJson)
    {
        var args = ParseArgs(argsJson, ProtocolJsonContext.Default.CodeArgs);
        if (args is null || string.IsNullOrEmpty(args.Code))
            return HostResponse.Fail("code 参数不能为空");
        var result = _engine!.Format(args.Code);
        return HostResponse.Ok(JsonSerializer.Serialize(result, ProtocolJsonContext.Default.FormatResult));
    }

    private static HostResponse SessionCreateTool()
    {
        var id = _engine!.SessionCreate();
        var result = new SessionCreateResult { SessionId = id };
        return HostResponse.Ok(JsonSerializer.Serialize(result, ProtocolJsonContext.Default.SessionCreateResult));
    }

    private static async Task<HostResponse> SessionExecAsync(string? argsJson)
    {
        var args = ParseArgs(argsJson, ProtocolJsonContext.Default.SessionExecArgs);
        if (args is null || string.IsNullOrEmpty(args.SessionId))
            return HostResponse.Fail("sessionId 参数不能为空");
        if (string.IsNullOrEmpty(args.Code))
            return HostResponse.Fail("code 参数不能为空");
        var result = await _engine!.SessionExecAsync(args.SessionId, args.Code, args.TimeoutMs, CancellationToken.None).ConfigureAwait(false);
        return HostResponse.Ok(JsonSerializer.Serialize(result, ProtocolJsonContext.Default.RunResult));
    }

    private static HostResponse SessionResetTool(string? argsJson)
    {
        var args = ParseArgs(argsJson, ProtocolJsonContext.Default.SessionIdArgs);
        if (args is null || string.IsNullOrEmpty(args.SessionId))
            return HostResponse.Fail("sessionId 参数不能为空");
        return _engine!.SessionReset(args.SessionId)
            ? HostResponse.Ok()
            : HostResponse.Fail($"会话不存在：{args.SessionId}");
    }

    private static HostResponse SessionCloseTool(string? argsJson)
    {
        var args = ParseArgs(argsJson, ProtocolJsonContext.Default.SessionIdArgs);
        if (args is null || string.IsNullOrEmpty(args.SessionId))
            return HostResponse.Fail("sessionId 参数不能为空");
        return _engine!.SessionClose(args.SessionId)
            ? HostResponse.Ok()
            : HostResponse.Fail($"会话不存在：{args.SessionId}");
    }

    private static T? ParseArgs<T>(string? json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        return JsonSerializer.Deserialize(json, typeInfo);
    }
}
