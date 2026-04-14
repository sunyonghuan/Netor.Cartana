using System.Runtime.InteropServices;

namespace Netor.Cortana.Pages;

/// <summary>
/// 设置窗体 — 管理智能体、模型、AI 厂商和参数微调。
/// </summary>
internal class SettingsWindow : Formedge
{
    public SettingsWindow()
    {
        Url = App.Map("settings.html");
        WindowTitle = "设置";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(820, 620);
        MinimumSize = new Size(680, 500);
        Maximizable = false;
        Icon = App.AppIcon;
        this.SetVirtualHostNameToEmbeddedResourcesMapping();

        Load += SettingsWindow_Load;
        FormClosing += SettingsWindow_FormClosing;
    }

    /// <summary>
    /// 拦截关闭事件，改为隐藏窗口而非销毁。
    /// 作为单例注册在 DI 容器中，Dispose 后无法再次使用。
    /// </summary>
    private void SettingsWindow_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.Visible = false;
        }
    }

    private void SettingsWindow_Load(object? sender, System.EventArgs e)
    {
        CoreWebView2?.AddHostObjectToScript("settingsBridge", new SettingsBridgeHostObject());
    }

    protected override WindowSettings ConfigureWindowSettings(HostWindowBuilder opts)
    {
        var settings = opts.UseDefaultWindow();
        settings.ExtendsContentIntoTitleBar = false;
        settings.ShowWindowDecorators = true;
        settings.SystemBackdropType = SystemBackdropType.Mica;
        return settings;
    }
}