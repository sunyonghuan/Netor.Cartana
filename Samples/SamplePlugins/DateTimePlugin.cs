using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Netor.Cortana.Plugin.Abstractions;

using System.Globalization;

namespace SamplePlugins;

/// <summary>
/// 日期时间插件 — 提供时区转换、日期计算、工作日判断等时间工具。
/// </summary>
public sealed class DateTimePlugin : IPlugin
{
    private readonly List<AITool> _tools = [];
    private ILogger? _logger;

    public string Id => "com.sample.datetime";
    public string Name => "日期时间";
    public Version Version => new(1, 0, 0);
    public string Description => "提供时区转换、日期差计算、工作日判断、Unix 时间戳转换等能力";
    public IReadOnlyList<string> Tags => ["时间", "工具"];
    public IReadOnlyList<AITool> Tools => _tools;

    public string? Instructions => """
        当用户询问日期、时间相关问题时，使用以下工具：
        - sys_dt_now: 获取当前时间（含多种格式）
        - sys_dt_timezone_convert: 在不同时区间转换时间
        - sys_dt_diff: 计算两个日期之间的差值
        - sys_dt_is_workday: 判断某天是否为工作日
        - sys_dt_countdown: 计算到目标日期的倒计时
        - sys_dt_unix_convert: Unix 时间戳与可读时间互转
        - sys_dt_format: 将日期时间格式化为指定格式
        """;

    public Task InitializeAsync(IPluginContext context)
    {
        _logger = context.LoggerFactory.CreateLogger<DateTimePlugin>();

        _tools.Add(AIFunctionFactory.Create(Now, "sys_dt_now", "获取当前日期时间（包含多种格式和时区信息）"));
        _tools.Add(AIFunctionFactory.Create(TimezoneConvert, "sys_dt_timezone_convert", "将时间从一个时区转换到另一个时区"));
        _tools.Add(AIFunctionFactory.Create(DateDiff, "sys_dt_diff", "计算两个日期之间的差值（天数、小时等）"));
        _tools.Add(AIFunctionFactory.Create(IsWorkday, "sys_dt_is_workday", "判断指定日期是否为工作日"));
        _tools.Add(AIFunctionFactory.Create(Countdown, "sys_dt_countdown", "计算从今天到目标日期的倒计时"));
        _tools.Add(AIFunctionFactory.Create(UnixConvert, "sys_dt_unix_convert", "Unix 时间戳与可读日期时间互转"));
        _tools.Add(AIFunctionFactory.Create(FormatDate, "sys_dt_format", "将日期时间字符串格式化为指定输出格式"));

        _logger.LogInformation("DateTimePlugin 初始化完成，注册 {Count} 个工具", _tools.Count);
        return Task.CompletedTask;
    }

    private string Now()
    {
        var now = DateTimeOffset.Now;
        var utcNow = DateTimeOffset.UtcNow;

        return $"""
            本地时间：{now:yyyy-MM-dd HH:mm:ss} ({now.Offset:hh\:mm})
            UTC 时间：{utcNow:yyyy-MM-dd HH:mm:ss}Z
            ISO 8601：{now:O}
            星期：{now.DayOfWeek}（{GetChineseDayOfWeek(now.DayOfWeek)}）
            今年第 {now.DayOfYear} 天
            Unix 时间戳：{now.ToUnixTimeSeconds()}
            Unix 毫秒戳：{now.ToUnixTimeMilliseconds()}
            """;
    }

    private string TimezoneConvert(string dateTime, string fromTimezone, string toTimezone)
    {
        try
        {
            var fromTz = TimeZoneInfo.FindSystemTimeZoneById(fromTimezone);
            var toTz = TimeZoneInfo.FindSystemTimeZoneById(toTimezone);

            if (!DateTimeOffset.TryParse(dateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
                return $"无法解析日期时间：{dateTime}";

            var fromTime = new DateTimeOffset(dto.DateTime, fromTz.BaseUtcOffset);
            var toTime = TimeZoneInfo.ConvertTime(fromTime, toTz);

            return $"{fromTimezone} {fromTime:yyyy-MM-dd HH:mm:ss} → {toTimezone} {toTime:yyyy-MM-dd HH:mm:ss}";
        }
        catch (TimeZoneNotFoundException ex)
        {
            return $"时区不存在：{ex.Message}\n常用时区：Asia/Shanghai, America/New_York, Europe/London, Asia/Tokyo, UTC";
        }
        catch (Exception ex)
        {
            return $"时区转换失败：{ex.Message}";
        }
    }

    private string DateDiff(string date1, string date2)
    {
        if (!DateTime.TryParse(date1, out var d1)) return $"无法解析日期：{date1}";
        if (!DateTime.TryParse(date2, out var d2)) return $"无法解析日期：{date2}";

        var diff = d2 - d1;
        var absDiff = diff.Duration();

        return $"""
            日期1：{d1:yyyy-MM-dd}
            日期2：{d2:yyyy-MM-dd}
            相差：{Math.Abs(diff.Days)} 天
            相差：{absDiff.TotalHours:F0} 小时
            相差：{absDiff.TotalMinutes:F0} 分钟
            方向：{(diff.TotalDays >= 0 ? "date2 在 date1 之后" : "date2 在 date1 之前")}
            """;
    }

    private string IsWorkday(string date)
    {
        if (!DateTime.TryParse(date, out var d)) return $"无法解析日期：{date}";

        var isWeekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var dayName = GetChineseDayOfWeek(d.DayOfWeek);

        return $"""
            日期：{d:yyyy-MM-dd}（{dayName}）
            类型：{(isWeekend ? "❌ 周末（非工作日）" : "✅ 工作日")}
            注意：此结果不包含法定节假日调休，仅根据星期判断
            """;
    }

    private string Countdown(string targetDate, string eventName = "目标日期")
    {
        if (!DateTime.TryParse(targetDate, out var target)) return $"无法解析日期：{targetDate}";

        var now = DateTime.Now.Date;
        var diff = target.Date - now;

        if (diff.TotalDays == 0)
            return $"🎯 {eventName}就是今天！（{target:yyyy-MM-dd}）";
        else if (diff.TotalDays > 0)
            return $"⏳ 距离 {eventName}（{target:yyyy-MM-dd}）还有 {diff.Days} 天（约 {diff.Days / 7} 周 {diff.Days % 7} 天）";
        else
            return $"📅 {eventName}（{target:yyyy-MM-dd}）已过去 {Math.Abs(diff.Days)} 天";
    }

    private string UnixConvert(string input, string action = "auto")
    {
        // 自动判断方向
        if (action == "auto")
            action = long.TryParse(input, out _) ? "to_date" : "to_unix";

        if (action == "to_date")
        {
            if (!long.TryParse(input, out var timestamp)) return $"无效的时间戳：{input}";

            // 判断秒还是毫秒
            DateTimeOffset dto;
            if (timestamp > 1_000_000_000_000)
                dto = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
            else
                dto = DateTimeOffset.FromUnixTimeSeconds(timestamp);

            return $"""
                时间戳：{input}
                UTC：{dto.UtcDateTime:yyyy-MM-dd HH:mm:ss}
                本地：{dto.LocalDateTime:yyyy-MM-dd HH:mm:ss}
                ISO：{dto:O}
                """;
        }
        else
        {
            if (!DateTimeOffset.TryParse(input, out var dto)) return $"无法解析日期：{input}";

            return $"""
                日期：{input}
                Unix 秒：{dto.ToUnixTimeSeconds()}
                Unix 毫秒：{dto.ToUnixTimeMilliseconds()}
                """;
        }
    }

    private string FormatDate(string dateTime, string format = "yyyy-MM-dd HH:mm:ss")
    {
        if (!DateTime.TryParse(dateTime, out var dt)) return $"无法解析日期：{dateTime}";

        try
        {
            return $"格式化结果：{dt.ToString(format, CultureInfo.InvariantCulture)}";
        }
        catch (FormatException)
        {
            return $"无效的格式字符串：{format}\n常用格式：yyyy-MM-dd, HH:mm:ss, yyyy/MM/dd, ddd MMM dd yyyy";
        }
    }

    private static string GetChineseDayOfWeek(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "星期一",
        DayOfWeek.Tuesday => "星期二",
        DayOfWeek.Wednesday => "星期三",
        DayOfWeek.Thursday => "星期四",
        DayOfWeek.Friday => "星期五",
        DayOfWeek.Saturday => "星期六",
        DayOfWeek.Sunday => "星期日",
        _ => day.ToString()
    };
}
