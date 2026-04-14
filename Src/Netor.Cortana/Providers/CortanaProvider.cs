using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Netor.Cortana.Pages;

namespace Netor.Cortana.Providers;

/// <summary>
/// 自我操作的工具提供者，提供窗口管理和 AI 配置管理能力。
/// </summary>
internal sealed class CortanaProvider(ILogger<CortanaProvider> logger) : AIContextProvider
{
    private readonly List<AITool> _tools = [];

    private AiProviderService ProviderService => App.Services.GetRequiredService<AiProviderService>();
    private AiModelService ModelService => App.Services.GetRequiredService<AiModelService>();
    private AgentService AgentService => App.Services.GetRequiredService<AgentService>();
    private IPublisher Publisher => App.Services.GetRequiredService<IPublisher>();

    /// <inheritdoc />
    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        if (_tools.Count == 0)
        {
            RegisterTools();
        }

        return ValueTask.FromResult(new AIContext
        {
            Instructions = BuildInstructions(),
            Tools = _tools
        });
    }

    // ──────── 工具注册 ────────

    private void RegisterTools()
    {
        // 窗口管理
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_show_main_window",
            description: "显示并激活主窗口（对话界面）。",
            method: ShowMainWindow));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_hide_main_window",
            description: "隐藏主窗口（最小化到托盘）。",
            method: HideMainWindow));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_show_settings_window",
            description: "打开设置窗口。",
            method: ShowSettingsWindow));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_show_float_window",
            description: "显示桌面浮动球窗口。",
            method: ShowFloatWindow));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_move_float_window",
            description: "移动浮动球到屏幕指定位置。参数 x、y 为屏幕像素坐标。",
            method: (int x, int y) => MoveFloatWindow(x, y)));

        // 窗口状态查询
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_get_main_window_status",
            description: "获取主窗口当前状态，包括是否可见、窗口位置和大小。用于在执行窗口操作后验证结果。",
            method: GetMainWindowStatus));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_get_settings_window_status",
            description: "获取设置窗口当前状态，包括是否已打开。用于在执行窗口操作后验证结果。",
            method: GetSettingsWindowStatus));

        // AI 配置查询
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_list_providers",
            description: "列出所有已启用的 AI 服务厂商，返回带序号的列表。用户可通过序号选择。",
            method: ListProviders));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_list_agents",
            description: "列出所有已启用的智能体，返回带序号的列表。用户可通过序号选择。",
            method: ListAgents));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_list_models",
            description: "列出当前默认厂商下所有已启用的模型，返回带序号的列表。用户可通过序号选择。",
            method: ListModels));

        // AI 配置设置
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_set_default_provider",
            description: "将指定序号的厂商设为默认。需要先调用 sys_list_providers 获取列表，再根据用户说的序号设置。参数：index（从1开始的序号）。",
            method: (int index) => SetDefaultProvider(index)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_set_default_agent",
            description: "将指定序号的智能体设为默认。需要先调用 sys_list_agents 获取列表，再根据用户说的序号设置。参数：index（从1开始的序号）。",
            method: (int index) => SetDefaultAgent(index)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_set_default_model",
            description: "将指定序号的模型设为默认。需要先调用 sys_list_models 获取列表，再根据用户说的序号设置。参数：index（从1开始的序号）。",
            method: (int index) => SetDefaultModel(index)));
    }

    // ──────── 窗口管理 ────────

    /// <summary>
    /// 显示并激活主窗口。
    /// </summary>
    private string ShowMainWindow()
    {
        try
        {
            var main = App.Services.GetRequiredService<MainWindow>();
            main.Invoke(() =>
            {
                main.Show();
                main.Activate();
            });

            return "✓ 主窗口已显示";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "显示主窗口失败");
            return $"✗ 显示主窗口失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 隐藏主窗口。
    /// </summary>
    private string HideMainWindow()
    {
        try
        {
            var main = App.Services.GetRequiredService<MainWindow>();
            main.Invoke(() => main.Visible = false);

            return "✓ 主窗口已隐藏";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "隐藏主窗口失败");
            return $"✗ 隐藏主窗口失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 打开设置窗口。
    /// </summary>
    private string ShowSettingsWindow()
    {
        try
        {
            var main = App.Services.GetRequiredService<MainWindow>();
            main.Invoke(() =>
            {
                var settings = App.Services.GetRequiredService<SettingsWindow>();
                settings.Show();
                settings.Activate();
            });

            return "✓ 设置窗口已打开";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "打开设置窗口失败");
            return $"✗ 打开设置窗口失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 显示浮动球窗口。
    /// </summary>
    private string ShowFloatWindow()
    {
        try
        {
            var floatWin = App.Services.GetRequiredService<FloatWindow>();
            floatWin.Invoke(() =>
            {
                floatWin.Show();
                floatWin.Activate();
            });

            return "✓ 浮动球已显示";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "显示浮动球失败");
            return $"✗ 显示浮动球失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 移动浮动球到指定屏幕坐标。
    /// </summary>
    private string MoveFloatWindow(int x, int y)
    {
        try
        {
            var floatWin = App.Services.GetRequiredService<FloatWindow>();
            floatWin.Invoke(() => floatWin.Location = new Point(x, y));

            return $"✓ 浮动球已移动到 ({x}, {y})";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "移动浮动球失败");
            return $"✗ 移动浮动球失败：{ex.Message}";
        }
    }

    // ──────── 窗口状态查询 ────────

    /// <summary>
    /// 获取主窗口当前状态。
    /// </summary>
    private string GetMainWindowStatus()
    {
        try
        {
            var main = App.Services.GetRequiredService<MainWindow>();
            return main.Invoke(() =>
            {
                var visible = main.Visible;
                var state = main.WindowState;
                var location = main.Location;
                var size = main.Size;

                return $"主窗口状态：可见={visible}, 窗口状态={state}, 位置=({location.X},{location.Y}), 大小={size.Width}x{size.Height}";
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取主窗口状态失败");
            return $"✗ 获取主窗口状态失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 获取设置窗口当前状态。
    /// </summary>
    private string GetSettingsWindowStatus()
    {
        try
        {
            var main = App.Services.GetRequiredService<MainWindow>();
            return main.Invoke(() =>
            {
                var settingsWindow = Application.OpenForms.OfType<SettingsWindow>().FirstOrDefault();
                if (settingsWindow is null || settingsWindow.IsDisposed)
                    return "设置窗口状态：未打开";

                return $"设置窗口状态：已打开, 位置=({settingsWindow.Location.X},{settingsWindow.Location.Y}), 大小={settingsWindow.Size.Width}x{settingsWindow.Size.Height}";
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取设置窗口状态失败");
            return $"✗ 获取设置窗口状态失败：{ex.Message}";
        }
    }

    // ──────── AI 配置查询 ────────

    /// <summary>
    /// 列出所有已启用的 AI 厂商（带序号）。
    /// </summary>
    private string ListProviders()
    {
        var list = ProviderService.GetAll();
        if (list.Count == 0)
            return "当前没有已启用的 AI 厂商。";

        var sb = new StringBuilder();
        sb.AppendLine("AI 厂商列表：");
        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i];
            var marker = p.IsDefault ? " ★默认" : "";
            sb.AppendLine($"  {i + 1}. {p.Name}{marker}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 列出所有已启用的智能体（带序号）。
    /// </summary>
    private string ListAgents()
    {
        var list = AgentService.GetAll();
        if (list.Count == 0)
            return "当前没有已启用的智能体。";

        var sb = new StringBuilder();
        sb.AppendLine("智能体列表：");
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            var marker = a.IsDefault ? " ★默认" : "";
            sb.AppendLine($"  {i + 1}. {a.Name}{marker}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 列出当前默认厂商下所有已启用的模型（带序号）。
    /// </summary>
    private string ListModels()
    {
        var providers = ProviderService.GetAll();
        var defaultProvider = providers.FirstOrDefault(p => p.IsDefault) ?? providers.FirstOrDefault();
        if (defaultProvider is null)
            return "没有可用的 AI 厂商，无法获取模型列表。";

        var list = ModelService.GetByProviderId(defaultProvider.Id);
        if (list.Count == 0)
            return $"厂商「{defaultProvider.Name}」下没有已启用的模型。";

        var sb = new StringBuilder();
        sb.AppendLine($"厂商「{defaultProvider.Name}」的模型列表：");
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            var displayName = string.IsNullOrWhiteSpace(m.DisplayName) ? m.Name : m.DisplayName;
            var marker = m.IsDefault ? " ★默认" : "";
            sb.AppendLine($"  {i + 1}. {displayName}{marker}");
        }

        return sb.ToString();
    }

    // ──────── AI 配置设置 ────────

    /// <summary>
    /// 根据序号设置默认厂商。
    /// </summary>
    private string SetDefaultProvider(int index)
    {
        var list = ProviderService.GetAll();
        if (index < 1 || index > list.Count)
            return $"✗ 无效的序号 {index}，有效范围 1~{list.Count}。";

        var target = list[index - 1];
        ProviderService.SetDefault(target.Id);

        Publisher.Publish(Events.OnAiProviderChange, new DataChangeArgs(target.Id, ChangeType.Update));
        logger.LogInformation("已将默认厂商设置为：{Name}", target.Name);

        return $"✓ 已将默认厂商设置为「{target.Name}」";
    }

    /// <summary>
    /// 根据序号设置默认智能体。
    /// </summary>
    private string SetDefaultAgent(int index)
    {
        var list = AgentService.GetAll();
        if (index < 1 || index > list.Count)
            return $"✗ 无效的序号 {index}，有效范围 1~{list.Count}。";

        var target = list[index - 1];
        AgentService.SetDefault(target.Id);

        Publisher.Publish(Events.OnAgentChange, new DataChangeArgs(target.Id, ChangeType.Update));
        logger.LogInformation("已将默认智能体设置为：{Name}", target.Name);

        return $"✓ 已将默认智能体设置为「{target.Name}」";
    }

    /// <summary>
    /// 根据序号设置默认模型。
    /// </summary>
    private string SetDefaultModel(int index)
    {
        var providers = ProviderService.GetAll();
        var defaultProvider = providers.FirstOrDefault(p => p.IsDefault) ?? providers.FirstOrDefault();
        if (defaultProvider is null)
            return "✗ 没有可用的 AI 厂商，无法设置默认模型。";

        var list = ModelService.GetByProviderId(defaultProvider.Id);
        if (index < 1 || index > list.Count)
            return $"✗ 无效的序号 {index}，有效范围 1~{list.Count}。";

        var target = list[index - 1];
        ModelService.SetDefault(target.Id);

        Publisher.Publish(Events.OnAiModelChange, new DataChangeArgs(target.Id, ChangeType.Update));
        logger.LogInformation("已将默认模型设置为：{Name}", target.Name);

        var displayName = string.IsNullOrWhiteSpace(target.DisplayName) ? target.Name : target.DisplayName;

        return $"✓ 已将默认模型设置为「{displayName}」";
    }

    // ──────── 指令构建 ────────

    /// <summary>
    /// 构建 AI 使用自我操作工具的指令。
    /// </summary>
    private static string BuildInstructions()
    {
        return """
            ### 自我操作工具使用规范

            你拥有控制自身应用窗口和管理 AI 配置的能力。

            #### 窗口管理
            - 用户要求打开/显示主界面时，调用 sys_show_main_window
            - 用户要求隐藏/关闭主界面时，调用 sys_hide_main_window
            - 用户要求打开设置时，调用 sys_show_settings_window
            - 用户要求显示/打开浮动球时，调用 sys_show_float_window
            - 用户要求移动浮动球时，调用 sys_move_float_window 并传入坐标
            - 执行窗口操作后，可调用 sys_get_main_window_status 或 sys_get_settings_window_status 验证操作结果
            - 用户询问窗口是否打开/关闭时，调用对应的状态查询工具

            #### AI 配置管理（序号交互模式）
            当用户想要查看或切换厂商/智能体/模型时：
            1. 先调用对应的 sys_list_* 工具获取列表，向用户展示带序号的列表
            2. 等待用户说出序号
            3. 用户说出序号后，调用对应的 sys_set_default_* 工具传入序号完成设置

            **重要：**
            - 用户说「切换厂商」「换个厂商」→ 先 sys_list_providers，展示列表等用户选择
            - 用户说「切换模型」「换个模型」→ 先 sys_list_models，展示列表等用户选择
            - 用户说「切换智能体」「换个助手」→ 先 sys_list_agents，展示列表等用户选择
            - 用户直接说序号（如「1」「第2个」「选3」）→ 根据上下文调用对应的 sys_set_default_* 工具
            - 列表中 ★ 标记表示当前默认项
            """;
    }
}