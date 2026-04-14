namespace Netor.Cortana;

/// <summary>
/// 窗口控制的独立实现，通过 DI 获取窗口实例。
/// 职责单一：仅负责窗口操作，与应用生命周期解耦。
/// </summary>
internal sealed class WindowController(
    IServiceProvider serviceProvider,
    IPublisher publisher) : IWindowController
{
    /// <inheritdoc />
    public void ShowMainWindow()
    {
        var main = serviceProvider.GetRequiredService<MainWindow>();
        main.Show();
        main.Activate();

        // 通知 Bubble 等订阅者：主窗口已显示
        publisher.Publish(Events.OnMainWindowShown, new VoiceSignalArgs());
    }

    /// <inheritdoc />
    public void HideMainWindow()
    {
        var main = serviceProvider.GetRequiredService<MainWindow>();
        main.Visible = false;
    }

    /// <inheritdoc />
    public void ShowSettingsWindow()
    {
        var settings = serviceProvider.GetRequiredService<SettingsWindow>();
        settings.Show();
        settings.Activate();
    }

    /// <inheritdoc />
    public void ShowFloatWindow()
    {
        serviceProvider.GetRequiredService<FloatWindow>().Show();
    }

    /// <inheritdoc />
    public void MoveFloatWindow(int x, int y)
    {
        serviceProvider.GetRequiredService<FloatWindow>().Location = new Point(x, y);
    }

    /// <inheritdoc />
    public bool IsMainWindowVisible()
    {
        return serviceProvider.GetRequiredService<MainWindow>().Visible;
    }
}
