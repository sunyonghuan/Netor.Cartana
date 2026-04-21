using Microsoft.Extensions.DependencyInjection;

namespace Netor.Cortana.Plugin.Process.Hosting;

/// <summary>
/// 用户在 <c>[Plugin] partial class</c> 中可选实现的 DI 配置方法。
/// <para>
/// 示例：
/// <code>
/// static partial void Configure(IServiceCollection services)
/// {
///     services.AddHttpClient();
///     services.AddSingleton&lt;IMyService, MyService&gt;();
/// }
/// </code>
/// </para>
/// 由 Generator 在 <c>Program.g.cs</c> 中以如下形式调用：
/// <code>
/// ConfigurePluginServices configure = MyPlugin.Configure;
/// configure?.Invoke(services);
/// </code>
/// </summary>
public delegate void ConfigurePluginServices(IServiceCollection services);
