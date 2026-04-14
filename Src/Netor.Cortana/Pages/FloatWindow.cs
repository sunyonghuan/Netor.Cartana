namespace Netor.Cortana.Pages;

/// <summary>
/// 圆形浮动窗口 - 桌面悬浮球。
/// </summary>
internal class FloatWindow : Formedge
{
    private const int FloatSize = 120;

    /// <summary>
    /// 窗口位置发生变化时触发，供气泡窗口跟随移动。
    /// </summary>
    internal event Action? PositionChanged;

    public FloatWindow()
    {
        Url = App.Map("float.html");
        WindowTitle = "小娜宝贝";
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(FloatSize, FloatSize);
        Maximizable = false;
        Minimizable = false;
        StartPosition = FormStartPosition.Manual;
        Icon = App.AppIcon;
        // 定位到屏幕右下角
        var workArea = Screen.PrimaryScreen!.Bounds;
        Location = new Point(
            workArea.Right - FloatSize,
            workArea.Bottom - FloatSize);
        this.SetVirtualHostNameToEmbeddedResourcesMapping();

        Load += FloatWindow_Load;
        Move += (_, _) => PositionChanged?.Invoke();
    }

    private void FloatWindow_Load(object? sender, System.EventArgs e)
    {
        CoreWebView2?.AddHostObjectToScript("floatBridge", new FloatBridgeHostObject(this));
    }

    /// <summary>
    /// 显示主窗口。
    /// </summary>
    internal void ShowMainWindow()
    {
        if (App.MainWindow is null) return;
        App.MainWindow.Show();
        App.MainWindow.Activate();
    }

    protected override WindowSettings ConfigureWindowSettings(HostWindowBuilder opts)
    {
        var settings = opts.UseDefaultWindow();
        settings.ExtendsContentIntoTitleBar = true;
        settings.ShowWindowDecorators = false;
        settings.SystemBackdropType = SystemBackdropType.None;
        settings.Resizable = false;
        BackColor = Color.Transparent;
        return settings;
    }
}