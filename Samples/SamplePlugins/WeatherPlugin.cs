using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Netor.Cortana.Plugin.Abstractions;

namespace SamplePlugins;

/// <summary>
/// 天气查询插件 — 提供天气、空气质量、紫外线等查询工具。
/// </summary>
public sealed class WeatherPlugin : IPlugin
{
    private readonly List<AITool> _tools = [];
    private ILogger? _logger;

    public string Id => "com.sample.weather";
    public string Name => "天气查询";
    public Version Version => new(1, 0, 0);
    public string Description => "提供天气预报、空气质量、紫外线指数等查询能力";
    public IReadOnlyList<string> Tags => ["天气", "生活"];
    public IReadOnlyList<AITool> Tools => _tools;

    public string? Instructions => """
        当用户询问天气相关问题时，使用以下工具：
        - sys_weather_current: 查询当前天气
        - sys_weather_forecast: 查询多日预报
        - sys_weather_air_quality: 查询空气质量 AQI
        - sys_weather_sunrise_sunset: 查询日出日落时间
        - sys_weather_uv_index: 查询紫外线指数
        - sys_weather_wind_alert: 查询风力预警
        - sys_weather_clothing_advice: 获取穿衣建议
        """;

    public Task InitializeAsync(IPluginContext context)
    {
        _logger = context.LoggerFactory.CreateLogger<WeatherPlugin>();

        _tools.Add(AIFunctionFactory.Create(GetCurrentWeather, "sys_weather_current", "查询指定城市的当前天气"));
        _tools.Add(AIFunctionFactory.Create(GetForecast, "sys_weather_forecast", "查询指定城市未来 N 天天气预报"));
        _tools.Add(AIFunctionFactory.Create(GetAirQuality, "sys_weather_air_quality", "查询指定城市的空气质量 AQI"));
        _tools.Add(AIFunctionFactory.Create(GetSunriseSunset, "sys_weather_sunrise_sunset", "查询指定城市的日出日落时间"));
        _tools.Add(AIFunctionFactory.Create(GetUvIndex, "sys_weather_uv_index", "查询指定城市的紫外线指数"));
        _tools.Add(AIFunctionFactory.Create(GetWindAlert, "sys_weather_wind_alert", "查询指定城市的风力预警信息"));
        _tools.Add(AIFunctionFactory.Create(GetClothingAdvice, "sys_weather_clothing_advice", "根据当前天气给出穿衣建议"));

        _logger.LogInformation("WeatherPlugin 初始化完成，注册 {Count} 个工具", _tools.Count);
        return Task.CompletedTask;
    }

    private string GetCurrentWeather(string city)
    {
        _logger?.LogDebug("查询 {City} 当前天气", city);
        var random = new Random();
        var temp = random.Next(-5, 40);
        var humidity = random.Next(20, 95);
        string[] conditions = ["晴", "多云", "阴", "小雨", "大雨", "雷阵雨", "雪", "雾霾"];
        var condition = conditions[random.Next(conditions.Length)];
        return $"{city}：{condition}，{temp}°C，湿度 {humidity}%，体感温度 {temp - 2}°C";
    }

    private string GetForecast(string city, int days = 3)
    {
        days = Math.Clamp(days, 1, 7);
        var random = new Random();
        var lines = new List<string> { $"{city}未来 {days} 天天气预报：" };
        string[] conditions = ["晴", "多云", "阴", "小雨", "中雨", "大雨"];

        for (int i = 1; i <= days; i++)
        {
            var date = DateTime.Now.AddDays(i);
            var high = random.Next(15, 38);
            var low = high - random.Next(5, 12);
            lines.Add($"  {date:MM-dd} {conditions[random.Next(conditions.Length)]} {low}°C ~ {high}°C");
        }

        return string.Join("\n", lines);
    }

    private string GetAirQuality(string city)
    {
        var random = new Random();
        var aqi = random.Next(15, 300);
        var level = aqi switch
        {
            <= 50 => "优",
            <= 100 => "良",
            <= 150 => "轻度污染",
            <= 200 => "中度污染",
            <= 300 => "重度污染",
            _ => "严重污染"
        };
        return $"{city} 空气质量 AQI: {aqi}（{level}），PM2.5: {random.Next(5, 200)}µg/m³";
    }

    private string GetSunriseSunset(string city)
    {
        var random = new Random();
        var sunrise = new TimeOnly(5 + random.Next(0, 2), random.Next(0, 60));
        var sunset = new TimeOnly(17 + random.Next(0, 3), random.Next(0, 60));
        return $"{city} 日出 {sunrise:HH:mm}，日落 {sunset:HH:mm}，日照时长 {(sunset.ToTimeSpan() - sunrise.ToTimeSpan()).TotalHours:F1} 小时";
    }

    private string GetUvIndex(string city)
    {
        var random = new Random();
        var uv = random.Next(0, 12);
        var level = uv switch
        {
            <= 2 => "低",
            <= 5 => "中等",
            <= 7 => "高",
            <= 10 => "很高",
            _ => "极高"
        };
        return $"{city} 紫外线指数: {uv}（{level}），建议{(uv > 5 ? "涂抹防晒霜并佩戴太阳镜" : "正常外出")}";
    }

    private string GetWindAlert(string city)
    {
        var random = new Random();
        var windSpeed = random.Next(0, 120);
        var level = windSpeed switch
        {
            <= 10 => "微风",
            <= 30 => "3-4级",
            <= 50 => "5-6级",
            <= 80 => "7-8级（大风预警）",
            _ => "9级以上（暴风预警）"
        };
        string[] directions = ["东风", "南风", "西风", "北风", "东北风", "西南风"];
        return $"{city} 风力: {level}，{directions[random.Next(directions.Length)]}，风速 {windSpeed}km/h" +
               (windSpeed > 50 ? "，⚠️ 建议减少外出" : "");
    }

    private string GetClothingAdvice(string city)
    {
        var random = new Random();
        var temp = random.Next(-5, 40);
        var advice = temp switch
        {
            <= 5 => "厚羽绒服 + 围巾手套，注意防寒保暖",
            <= 15 => "外套或薄羽绒服，可搭配毛衣",
            <= 25 => "长袖衬衫或薄外套，舒适为主",
            <= 32 => "短袖 T恤、短裤，注意防晒",
            _ => "尽量待在室内，外出注意防暑降温"
        };
        return $"{city} 当前 {temp}°C，穿衣建议：{advice}";
    }
}
