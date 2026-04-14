using System.Text;

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

using Netor.Cortana.AvaloniaUI.Controls;
using Netor.Cortana.AvaloniaUI.Views;

namespace Netor.Cortana.AvaloniaUI;

/// <summary>
/// Avalonia UI 输出通道。将 AI 流式回复实时渲染到 MainWindow 的消息列表中。
/// 每次对话开始时创建一个 AI 消息气泡，后续 token 累积更新该气泡的 Markdown 内容。
/// </summary>
internal sealed class UiChatOutputChannel(
    IServiceProvider serviceProvider,
    ILogger<UiChatOutputChannel> logger) : IAiOutputChannel
{
    private readonly StringBuilder _buffer = new();
    private MarkdownRenderer? _currentPresenter;

    /// <inheritdoc />
    public string Name => "AvaloniaUI";

    /// <inheritdoc />
    public bool IsActive => true;

    /// <inheritdoc />
    public Task OnTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        _buffer.Append(token);
        var currentText = _buffer.ToString();

        Dispatcher.UIThread.Post(() =>
        {
            EnsureBubbleCreated();
            if (_currentPresenter is not null)
            {
                _currentPresenter.Markdown = currentText;
            }

            // 流式 token 期间自动跟随滚动（尊重用户上滚）
            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.AutoScrollToBottom();
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnDoneAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var finalText = _buffer.ToString();

        Dispatcher.UIThread.Post(() =>
        {
            // 最终刷新一次确保完整内容（跳过防抖立即渲染）
            if (_currentPresenter is not null && !string.IsNullOrEmpty(finalText))
            {
                _currentPresenter.FlushRender();
                _currentPresenter.Markdown = finalText;
            }

            ScrollToBottom(force: true);

            // 必须在 UI 线程回调内重置，否则 Post 异步导致 _currentPresenter 提前被置空
            Reset();
        });

        logger.LogDebug("UI 输出通道完成，Session：{SessionId}", sessionId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnCancelledAsync()
    {
        logger.LogDebug("UI 输出通道已取消");
        Dispatcher.UIThread.Post(Reset);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnErrorAsync(string message, CancellationToken cancellationToken = default)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.AddMessageBubble($"⚠ {message}", isUser: false);
        });

        logger.LogWarning("UI 输出通道收到错误：{Message}", message);
        Reset();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 确保已创建 AI 回复气泡。首次收到 token 时创建。
    /// 布局与 MainWindow.AddMessageBubble 保持一致：DockPanel(头像 + 气泡)。
    /// </summary>
    private void EnsureBubbleCreated()
    {
        if (_currentPresenter is not null) return;

        var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        var messageList = mainWindow.FindControl<ItemsControl>("MessageList");
        if (messageList is null) return;

        // ── AI 头像 (32px 圆形渐变) ──
        var avatar = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 10, 0),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#007acc"), 0),
                    new GradientStop(Color.Parse("#3794ff"), 1)
                }
            },
            Child = new TextBlock
            {
                Text = "AI",
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }
        };

        _currentPresenter = new MarkdownRenderer
        {
            Markdown = "",
        };

        // ── 气泡容器 ──
        var bubble = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            CornerRadius = new CornerRadius(4, 3, 3, 3),
            Padding = new Thickness(14, 10),
            MinWidth = 120,
            Margin = new Thickness(0, 0, 42, 0),
            Child = _currentPresenter,
        };

        // ── 消息行：头像(Left) + 气泡(Fill) ──
        var row = new DockPanel { LastChildFill = true };
        avatar.SetValue(DockPanel.DockProperty, Dock.Left);
        row.Children.Add(avatar);
        row.Children.Add(bubble);

        messageList.Items.Add(row);
        ScrollToBottom();
    }

    /// <summary>
    /// 滚动消息列表到底部。
    /// </summary>
    private void ScrollToBottom(bool force = false)
    {
        var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        if (force)
            mainWindow.ForceScrollToBottom();
        else
            mainWindow.AutoScrollToBottom();
    }

    /// <summary>
    /// 重置通道状态，为下一轮对话做准备。
    /// </summary>
    private void Reset()
    {
        _buffer.Clear();
        _currentPresenter = null;
    }
}
