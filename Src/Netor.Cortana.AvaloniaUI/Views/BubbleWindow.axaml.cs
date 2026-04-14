using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

using Netor.EventHub;

namespace Netor.Cortana.AvaloniaUI.Views;

/// <summary>
/// 唤醒词触发后显示的字幕气泡窗口（纯 XAML 实现）。
/// 显示在浮动窗口上方，跟随 FloatWindow 移动。
/// 纯事件驱动：通过 EventHub 订阅语音流程事件控制显示/更新/关闭。
/// </summary>
public partial class BubbleWindow : Window
{
    private const int BubbleWidth = 360;
    private const int BubbleHeight = 99;
    private const int BubbleMaxHeight = 360;
    private const int GapFromFloat = 10;
    private const int DismissDelayMs = 1500;

    private FloatWindow? _anchorWindow;
    private ILogger<BubbleWindow> Logger => App.Services.GetRequiredService<ILogger<BubbleWindow>>();

    public BubbleWindow()
    {
        InitializeComponent();
        SubscribeEvents();
    }

    /// <summary>
    /// 设置锚点窗口引用，Bubble 将跟随其位置显示。
    /// </summary>
    internal void SetAnchorWindow(FloatWindow floatWindow)
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
            Logger.LogInformation("收到唤醒词事件，显示气泡窗口。");
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
                SubtitleText.Text = args.Text;
                SubtitleText.Foreground = Brushes.White;
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

        // 主窗口被显示 → 关闭 Bubble
        subscriber.Subscribe<VoiceSignalArgs>(Events.OnMainWindowShown, (_, _) =>
        {
            PostToUI(Dismiss);
            return Task.FromResult(false);
        });

        // AI 推理完成 → 仅在主窗口可见时关闭 Bubble
        subscriber.Subscribe<VoiceSignalArgs>(Events.OnAiCompleted, (_, _) =>
        {
            PostToUI(() =>
            {
                var windowController = App.Services.GetRequiredService<IWindowController>();
                if (windowController.IsMainWindowVisible())
                {
                    Dismiss();
                }
            });
            return Task.FromResult(false);
        });
    }

    // ──────────────────── 内部方法 ────────────────────

    /// <summary>
    /// 将操作调度到 UI 线程执行。
    /// </summary>
    private static void PostToUI(Action action)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                action();
            }
            catch (ObjectDisposedException) { }
        });
    }

    /// <summary>
    /// 显示 Bubble 并进入监听状态。
    /// </summary>
    private void ShowBubble()
    {
        Width = BubbleWidth;
        Height = BubbleHeight;
        RepositionAboveAnchor();

        SubtitleText.Text = "正在聆听...";
        SubtitleText.Foreground = new SolidColorBrush(Color.FromRgb(170, 184, 232));
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(86, 156, 214));

        if (!IsVisible)
        {
            Show();
        }
    }

    /// <summary>
    /// 更新字幕文本。
    /// </summary>
    private void UpdateSubtitle(string text)
    {
        SubtitleText.Text = text;
        SubtitleText.Foreground = Brushes.White;

        // 根据文本长度调整高度
        AdjustHeight();
    }

    /// <summary>
    /// 播放淡出后隐藏窗口。
    /// </summary>
    private async void Dismiss()
    {
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(100, 100, 100));

        try
        {
            await Task.Delay(DismissDelayMs);
            PostToUI(() =>
            {
                Opacity = 0;
                _ = Task.Delay(300).ContinueWith(_ =>
                {
                    PostToUI(() =>
                    {
                        Hide();
                        Opacity = 1;
                    });
                }, TaskScheduler.Default);
            });
        }
        catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// 锚点窗口移动时跟随重定位。
    /// </summary>
    internal void OnAnchorMoved()
    {
        if (!IsVisible || _anchorWindow is null) return;
        PostToUI(RepositionAboveAnchor);
    }

    /// <summary>
    /// 将气泡窗口定位到锚点窗口正上方。
    /// </summary>
    private void RepositionAboveAnchor()
    {
        if (_anchorWindow is null) return;

        var anchorPos = _anchorWindow.Position;
        int x = anchorPos.X + ((int)_anchorWindow.Width - (int)Width) / 2;
        int y = anchorPos.Y - (int)Height - GapFromFloat;

        var screen = _anchorWindow.Screens.Primary;
        if (screen is not null)
        {
            var workArea = screen.WorkingArea;
            if (y < workArea.Y)
                y = workArea.Y;
        }

        Position = new PixelPoint(x, y);
    }

    /// <summary>
    /// 根据文本内容调整窗口高度。
    /// </summary>
    private void AdjustHeight()
    {
        // 基于文字长度估算行数，调整窗口高度
        var textLength = SubtitleText.Text?.Length ?? 0;
        var estimatedLines = Math.Max(1, textLength / 30);
        var desiredHeight = Math.Clamp(60 + estimatedLines * 22, BubbleHeight, BubbleMaxHeight);

        Height = desiredHeight;
        RepositionAboveAnchor();
    }
}