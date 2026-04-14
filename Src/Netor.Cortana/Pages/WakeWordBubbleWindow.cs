using Serilog.Core;

namespace Netor.Cortana.Pages;

/// <summary>
/// 唤醒词触发后显示的气泡窗口（Formedge 实现）。
/// 使用 HTML/CSS 渲染圆角、阴影、动画效果，显示在浮动窗口上方，跟随 FloatWindow 移动。
/// 纯事件驱动：通过 EventHub 订阅语音流程事件控制显示/更新/关闭，不使用自管理定时器。
/// </summary>
internal sealed class WakeWordBubbleWindow : Formedge
{
    private const int BubbleWidth = 360;
    private const int BubbleHeight = 99;
    private const int BubbleMaxHeight = 360;
    private const int GapFromFloat = 10;
    private const int DismissDelayMs = 1500;

    private readonly SynchronizationContext _uiContext;
    private Formedge? _anchorWindow;
    private bool _isDomReady;
    private ILogger<WakeWordBubbleWindow> logger => App.Services.GetRequiredService<ILogger<WakeWordBubbleWindow>>();

    public WakeWordBubbleWindow()
    {
        Url = App.Map("bubble.html");
        ShowInTaskbar = false;
        TopMost = true;
        Maximizable = false;
        Minimizable = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(BubbleWidth, BubbleHeight);
        BackColor = Color.Transparent;

        this.SetVirtualHostNameToEmbeddedResourcesMapping();

        DOMContentLoaded += OnDomContentLoaded;

        _uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("WakeWordBubbleWindow 必须在 UI 线程上创建。");

        SubscribeEvents();
    }

    /// <summary>
    /// DOM 加载完毕后标记就绪。
    /// </summary>
    private void OnDomContentLoaded(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2DOMContentLoadedEventArgs e)
    {
        _isDomReady = true;
    }

    /// <summary>
    /// 设置锚点窗口引用，Bubble 将跟随其位置显示。
    /// </summary>
    internal void SetAnchorWindow(Formedge floatWindow)
    {
        _anchorWindow = floatWindow;
    }

    /// <summary>
    /// 订阅语音流程 EventHub 事件，驱动 Bubble 生命周期。
    /// </summary>
    private void SubscribeEvents()
    {
        var subscriber = App.Services.GetRequiredService<ISubscriber>();

        // 唤醒词 → 显示 Bubble，进入监听状态
        subscriber.On(Events.OnWakeWordDetected, (_, _) =>
        {
            logger.LogInformation("收到唤醒词事件，显示气泡窗口。");
            PostToUI(ShowBubble);
            return Task.FromResult(false);
        });

        // STT 中间结果 → 更新字幕
        subscriber.Subscribe<VoiceTextArgs>(Events.OnSttPartial, (_, args) =>
        {
            PostToUI(() => UpdateSubtitle(args.Text));
            return Task.FromResult(false);
        });

        // STT 最终结果 → 显示最终文本
        subscriber.Subscribe<VoiceTextArgs>(Events.OnSttFinal, (_, args) =>
        {
            PostToUI(() =>
            {
                ExecuteScript($"setFinalResult({EscapeJs(args.Text)})");
                AdjustHeightAsync();
            });
            return Task.FromResult(false);
        });

        // STT 结束（超时/无内容） → 关闭 Bubble
        subscriber.Subscribe<VoiceSignalArgs>(Events.OnSttStopped, (_, _) =>
        {
            PostToUI(Dismiss);
            return Task.FromResult(false);
        });

        // TTS 开始播放 → 显示 Bubble
        subscriber.Subscribe<VoiceSignalArgs>(Events.OnTtsStarted, (_, _) =>
        {
            PostToUI(ShowBubble);
            return Task.FromResult(false);
        });

        // TTS 字幕 → 更新当前播放的句子
        subscriber.Subscribe<VoiceTextArgs>(Events.OnTtsSubtitle, (_, args) =>
        {
            PostToUI(() => UpdateSubtitle(args.Text));
            return Task.FromResult(false);
        });

        // 主窗口被显示 → 关闭 Bubble（输出已切换到主窗口）
        subscriber.Subscribe<VoiceSignalArgs>(Events.OnMainWindowShown, (_, _) =>
        {
            PostToUI(Dismiss);
            return Task.FromResult(false);
        });

        // AI 推理完成 → 仅在主窗口可见时关闭 Bubble（主窗口不可见时让 TTS 字幕继续显示）
        subscriber.Subscribe<VoiceSignalArgs>(Events.OnAiCompleted, (_, _) =>
        {
            var windowController = App.Services.GetRequiredService<IWindowController>();
            if (windowController.IsMainWindowVisible())
            {
                PostToUI(Dismiss);
            }
            return Task.FromResult(false);
        });
    }

    // ──────────────────── 内部方法 ────────────────────

    /// <summary>
    /// 将操作调度到 UI 线程执行，确保 WebView2 和窗口操作的线程安全。
    /// 不依赖 HostWindow 句柄是否已创建，适用于窗口尚未 Show 的场景。
    /// </summary>
    private void PostToUI(Action action)
    {
        _uiContext.Post(_ =>
        {
            try
            {
                action();
            }
            catch (ObjectDisposedException) { }
        }, null);
    }

    /// <summary>
    /// 显示 Bubble 并进入监听状态（必须在 UI 线程调用）。
    /// </summary>
    private void ShowBubble()
    {
        Size = new Size(BubbleWidth, BubbleHeight);
        RepositionAboveAnchor();

        if (!Visible)
        {
            Show();
        }

        ExecuteScript("setListening()");
    }

    /// <summary>
    /// 更新字幕文本（必须在 UI 线程调用）。
    /// </summary>
    private void UpdateSubtitle(string text)
    {
        ExecuteScript($"setPartialResult({EscapeJs(text)})");
        AdjustHeightAsync();
    }

    /// <summary>
    /// 播放淡出动画后隐藏窗口（必须在 UI 线程调用）。
    /// </summary>
    private async void Dismiss()
    {
        ExecuteScript("setDismissed()");

        try
        {
            await Task.Delay(DismissDelayMs);
            PostToUI(() =>
            {
                ExecuteScript("fadeOut()");

                _ = Task.Delay(300).ContinueWith(_ =>
                {
                    PostToUI(() => Visible = false);
                }, TaskScheduler.Default);
            });
        }
        catch (ObjectDisposedException) { }
    }

    internal void OnAnchorMoved()
    {
        if (!Visible || _anchorWindow is null) return;

        if (InvokeRequired)
        {
            Invoke(OnAnchorMoved);
            return;
        }

        RepositionAboveAnchor();
    }

    /// <summary>
    /// 将气泡窗口定位到锚点窗口正上方。
    /// </summary>
    private void RepositionAboveAnchor()
    {
        if (_anchorWindow is null) return;

        int x = _anchorWindow.Left + (_anchorWindow.Width - Width) / 2;
        int y = _anchorWindow.Top - Height - GapFromFloat;

        var workArea = Screen.FromHandle(_anchorWindow.Handle).WorkingArea;
        if (y < workArea.Top)
            y = workArea.Top;

        Location = new Point(x, y);
    }

    /// <summary>
    /// 从 JS 获取内容实际高度，动态调整窗口大小。
    /// </summary>
    private async void AdjustHeightAsync()
    {
        if (!_isDomReady || CoreWebView2 is null) return;

        try
        {
            string result = await CoreWebView2.ExecuteScriptAsync("getDesiredHeight()");
            if (int.TryParse(result, out int desiredHeight) && desiredHeight > 0)
            {
                int clampedHeight = Math.Clamp(desiredHeight, BubbleHeight, BubbleMaxHeight);
                Size = new Size(BubbleWidth, clampedHeight);
                RepositionAboveAnchor();
            }
        }
        catch
        {
            // 忽略脚本执行异常（窗口可能已关闭）
        }
    }

    /// <summary>
    /// 在 WebView2 中执行 JavaScript（安全封装）。
    /// </summary>
    private void ExecuteScript(string script)
    {
        if (!_isDomReady || CoreWebView2 is null) return;
        try
        {
            CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute script: {Script}", script);
        }
    }

    /// <summary>
    /// 将 C# 字符串转义为 JS 字符串字面量。
    /// </summary>
    private static string EscapeJs(string text)
    {
        string escaped = text
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");

        return $"'{escaped}'";
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