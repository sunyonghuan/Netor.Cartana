using System.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

using Netor.Cortana.Plugin.BuiltIn.PowerShell;

namespace PowerShell.Test;

sealed class TestCase
{
    public TestCase(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public bool Passed { get; private set; } = true;

    public string Message { get; private set; } = string.Empty;

    public void Assert(bool condition, string failureMessage)
    {
        if (!condition)
        {
            Passed = false;
            Message = failureMessage;
        }
    }

    public void Succeed(string message)
    {
        if (Passed)
        {
            Message = message;
        }
    }
}

public static class Program
{
    public static async Task<int> Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var tests = new List<TestCase>
        {
            await VerifyExecutorIgnoresCancelledCallerTokenAsync(),
            await VerifyExecutorTimeoutStillWorksAsync(),
            await VerifySessionIgnoresCancelledCallerTokenAsync(),
        };

        foreach (var test in tests)
        {
            Console.WriteLine($"[{(test.Passed ? "PASS" : "FAIL")}] {test.Name}: {test.Message}");
        }

        return tests.All(t => t.Passed) ? 0 : 1;
    }

    private static async Task<TestCase> VerifyExecutorIgnoresCancelledCallerTokenAsync()
    {
        var test = new TestCase("executor ignores cancelled caller token");
        await using var executor = new PowerShellExecutor(NullLogger<PowerShellExecutor>.Instance);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var stopwatch = Stopwatch.StartNew();
        var result = await executor.ExecuteAsync("Write-Output 'executor-ok'", 5000, cancelled.Token);
        stopwatch.Stop();

        test.Assert(result.Success, "expected executor command to succeed");
        test.Assert(result.Output.Contains("executor-ok", StringComparison.Ordinal), "expected executor output to contain marker");
        test.Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "expected executor command to finish before timeout");
        test.Succeed($"exit={result.ExitCode}, elapsed={stopwatch.ElapsedMilliseconds}ms");
        return test;
    }

    private static async Task<TestCase> VerifyExecutorTimeoutStillWorksAsync()
    {
        var test = new TestCase("executor timeout still terminates process");
        await using var executor = new PowerShellExecutor(NullLogger<PowerShellExecutor>.Instance);

        var result = await executor.ExecuteAsync("Start-Sleep -Seconds 3; Write-Output 'late'", 200, CancellationToken.None);

        test.Assert(!result.Success, "expected timed out command to fail");
        test.Assert(result.ExitCode == -1, "expected timeout exit code -1");
        test.Assert(result.Error.Contains("执行超时", StringComparison.Ordinal), "expected timeout message in error output");
        test.Succeed(result.Error.Trim());
        return test;
    }

    private static async Task<TestCase> VerifySessionIgnoresCancelledCallerTokenAsync()
    {
        var test = new TestCase("session ignores cancelled caller token");
        await using var session = new ExecutionSession("local", null, null, null, null, NullLogger<SessionRegistry>.Instance);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var output = new List<string>();
        await foreach (var line in session.ExecuteCommandAsync("Write-Output 'session-ok'", 5000, cancelled.Token))
        {
            output.Add(line);
        }

        var combined = string.Join(Environment.NewLine, output);
        test.Assert(session.IsActive, "expected session to remain active after command");
        test.Assert(combined.Contains("session-ok", StringComparison.Ordinal), "expected session output to contain marker");
        test.Succeed("command completed with active session");
        return test;
    }
}