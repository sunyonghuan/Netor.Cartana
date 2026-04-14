using Avalonia.Controls;
using Avalonia.Input;

namespace Netor.Cortana.AvaloniaUI.Views;

/// <summary>
/// 圆形浮动窗口 — 桌面悬浮球。
/// 纯 XAML 实现，无 WebView2，无 Chromium Renderer 进程开销。
/// </summary>
public partial class FloatWindow : Window
{
    private const int FloatSize = 80;

    /// <summary>
    /// 窗口位置发生变化时触发，供气泡窗口跟随移动。
    /// </summary>
    internal event Action? FloatPositionChanged;

    public FloatWindow()
    {
        InitializeComponent();

        // 定位到屏幕右下角（留出 120px 边距）
        var screen = Screens.Primary;
        if (screen is not null)
        {
            const int margin = 120;
            var workArea = screen.WorkingArea;
            Position = new PixelPoint(
                workArea.Right - FloatSize - margin,
                workArea.Bottom - FloatSize - margin);
        }

        PointerPressed += OnPointerPressed;
        DoubleTapped += OnDoubleTapped;

        // 监听 Window 基类的 PositionChanged 事件
        base.PositionChanged += (_, _) => FloatPositionChanged?.Invoke();
    }

    // ──────── 拖拽移动（使用操作系统原生拖拽） ────────

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    // ──────── 双击唤起主窗口 ────────

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        ShowMainWindow();
    }

    /// <summary>
    /// 显示主窗口。
    /// </summary>
    internal void ShowMainWindow()
    {
        var controller = App.Services.GetRequiredService<IWindowController>();
        controller.ShowMainWindow();
    }
}
