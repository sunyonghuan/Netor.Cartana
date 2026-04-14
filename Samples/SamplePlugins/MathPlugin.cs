using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Netor.Cortana.Plugin.Abstractions;

using System.Globalization;
using System.Text;

namespace SamplePlugins;

/// <summary>
/// 数学计算插件 — 提供运算、单位换算、进制转换等数学工具。
/// </summary>
public sealed class MathPlugin : IPlugin
{
    private readonly List<AITool> _tools = [];
    private ILogger? _logger;

    public string Id => "com.sample.math";
    public string Name => "数学计算";
    public Version Version => new(1, 0, 0);
    public string Description => "提供数学运算、单位换算、进制转换、统计分析等计算能力";
    public IReadOnlyList<string> Tags => ["数学", "科学"];
    public IReadOnlyList<AITool> Tools => _tools;

    public string? Instructions => """
        当用户需要数学计算时，使用以下工具：
        - sys_math_calculate: 基础四则运算和幂运算
        - sys_math_unit_convert: 单位换算（长度、重量、温度）
        - sys_math_base_convert: 进制转换（二/八/十/十六进制）
        - sys_math_statistics: 对一组数字做统计摘要
        - sys_math_fibonacci: 计算斐波那契数列
        - sys_math_gcd: 计算最大公约数和最小公倍数
        - sys_math_is_prime: 判断一个数是否为质数
        """;

    public Task InitializeAsync(IPluginContext context)
    {
        _logger = context.LoggerFactory.CreateLogger<MathPlugin>();

        _tools.Add(AIFunctionFactory.Create(Calculate, "sys_math_calculate", "执行基础数学运算（加减乘除、幂、开方、取余）"));
        _tools.Add(AIFunctionFactory.Create(UnitConvert, "sys_math_unit_convert", "单位换算（长度/重量/温度）"));
        _tools.Add(AIFunctionFactory.Create(BaseConvert, "sys_math_base_convert", "数字进制转换"));
        _tools.Add(AIFunctionFactory.Create(Statistics, "sys_math_statistics", "对一组数字计算统计摘要（均值/中位数/方差/极值）"));
        _tools.Add(AIFunctionFactory.Create(Fibonacci, "sys_math_fibonacci", "计算斐波那契数列前 N 项"));
        _tools.Add(AIFunctionFactory.Create(Gcd, "sys_math_gcd", "计算两个数的最大公约数和最小公倍数"));
        _tools.Add(AIFunctionFactory.Create(IsPrime, "sys_math_is_prime", "判断一个正整数是否为质数"));

        _logger.LogInformation("MathPlugin 初始化完成，注册 {Count} 个工具", _tools.Count);
        return Task.CompletedTask;
    }

    private string Calculate(string operation, double a, double b = 0)
    {
        var result = operation.ToLowerInvariant() switch
        {
            "add" or "+" => $"{a} + {b} = {a + b}",
            "subtract" or "-" => $"{a} - {b} = {a - b}",
            "multiply" or "*" or "×" => $"{a} × {b} = {a * b}",
            "divide" or "/" or "÷" => b == 0 ? "错误：除数不能为零" : $"{a} ÷ {b} = {a / b:G10}",
            "power" or "^" or "pow" => $"{a} ^ {b} = {Math.Pow(a, b):G10}",
            "sqrt" => a < 0 ? "错误：不能对负数开方" : $"√{a} = {Math.Sqrt(a):G10}",
            "mod" or "%" => b == 0 ? "错误：除数不能为零" : $"{a} % {b} = {a % b}",
            "abs" => $"|{a}| = {Math.Abs(a)}",
            _ => $"不支持的运算: {operation}（支持: add/subtract/multiply/divide/power/sqrt/mod/abs）"
        };

        _logger?.LogDebug("计算 {Op}({A}, {B}) → {Result}", operation, a, b, result);
        return result;
    }

    private string UnitConvert(double value, string from, string to)
    {
        double result;

        // 温度特殊处理
        var key = $"{from.ToLowerInvariant()}->{to.ToLowerInvariant()}";
        if (key is "c->f" or "celsius->fahrenheit")
            return $"{value}°C = {value * 9 / 5 + 32:F2}°F";
        if (key is "f->c" or "fahrenheit->celsius")
            return $"{value}°F = {(value - 32) * 5 / 9:F2}°C";
        if (key is "c->k" or "celsius->kelvin")
            return $"{value}°C = {value + 273.15:F2}K";
        if (key is "k->c" or "kelvin->celsius")
            return $"{value}K = {value - 273.15:F2}°C";

        // 长度（以米为基准）
        var lengthToMeter = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["mm"] = 0.001, ["cm"] = 0.01, ["m"] = 1, ["km"] = 1000,
            ["inch"] = 0.0254, ["ft"] = 0.3048, ["mile"] = 1609.344
        };

        // 重量（以克为基准）
        var weightToGram = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["mg"] = 0.001, ["g"] = 1, ["kg"] = 1000,
            ["oz"] = 28.3495, ["lb"] = 453.592, ["ton"] = 1_000_000
        };

        if (lengthToMeter.TryGetValue(from, out var fromM) && lengthToMeter.TryGetValue(to, out var toM))
        {
            result = value * fromM / toM;
            return $"{value} {from} = {result:G10} {to}";
        }

        if (weightToGram.TryGetValue(from, out var fromG) && weightToGram.TryGetValue(to, out var toG))
        {
            result = value * fromG / toG;
            return $"{value} {from} = {result:G10} {to}";
        }

        return $"不支持的单位转换：{from} → {to}";
    }

    private string BaseConvert(string number, int fromBase, int toBase)
    {
        try
        {
            var decimalValue = Convert.ToInt64(number.Trim(), fromBase);
            var converted = Convert.ToString(decimalValue, toBase).ToUpperInvariant();
            string fromName = BaseName(fromBase);
            string toName = BaseName(toBase);
            return $"{fromName} {number} = {toName} {converted}";
        }
        catch (Exception ex)
        {
            return $"转换失败：{ex.Message}";
        }

        static string BaseName(int b) => b switch
        {
            2 => "二进制", 8 => "八进制", 10 => "十进制", 16 => "十六进制", _ => $"{b}进制"
        };
    }

    private string Statistics(string numbers)
    {
        var values = numbers.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? (double?)v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        if (values.Count == 0) return "错误：未提供有效数字";

        var sorted = values.OrderBy(v => v).ToList();
        var mean = values.Average();
        var median = sorted.Count % 2 == 0
            ? (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2
            : sorted[sorted.Count / 2];
        var variance = values.Average(v => Math.Pow(v - mean, 2));

        return $"""
            数据：{string.Join(", ", values)}
            数量：{values.Count}
            均值：{mean:F4}
            中位数：{median:F4}
            方差：{variance:F4}
            标准差：{Math.Sqrt(variance):F4}
            最小值：{sorted[0]}
            最大值：{sorted[^1]}
            """;
    }

    private string Fibonacci(int n)
    {
        n = Math.Clamp(n, 1, 50);
        var fib = new List<long> { 0, 1 };
        for (int i = 2; i < n; i++)
            fib.Add(fib[i - 1] + fib[i - 2]);

        return $"斐波那契前 {n} 项：{string.Join(", ", fib.Take(n))}";
    }

    private string Gcd(long a, long b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        if (a == 0 && b == 0) return "错误：两个数不能同时为零";

        long gcd = GcdHelper(a, b);
        long lcm = a / gcd * b;

        return $"GCD({a}, {b}) = {gcd}，LCM({a}, {b}) = {lcm}";

        static long GcdHelper(long x, long y) => y == 0 ? x : GcdHelper(y, x % y);
    }

    private string IsPrime(long number)
    {
        if (number < 2) return $"{number} 不是质数（质数必须大于 1）";
        if (number <= 3) return $"{number} 是质数";
        if (number % 2 == 0) return $"{number} 不是质数（能被 2 整除）";
        if (number % 3 == 0) return $"{number} 不是质数（能被 3 整除）";

        for (long i = 5; i * i <= number; i += 6)
        {
            if (number % i == 0) return $"{number} 不是质数（能被 {i} 整除）";
            if (number % (i + 2) == 0) return $"{number} 不是质数（能被 {i + 2} 整除）";
        }

        return $"{number} 是质数 ✅";
    }
}
