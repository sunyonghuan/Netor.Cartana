using System.Runtime.InteropServices;

namespace Netor.Cortana.Pages;

internal partial class MainWindow : Formedge
{
    private NotifyIcon _notifyIcon = null!;
    private ISubscriber? _subscriber;

    private bool _forceClose;

    public MainWindow()
    {
        // 设置浏览器加载的 URL
        Url = App.Map("index.html");

        // 窗口属性
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(600, 1080);
        MinimumSize = new Size(400, 600);
        // 窗口标题
        WindowTitle = "小娜宝贝";
        // 是否允许全屏
        AllowFullscreen = true;
        // 禁用 WebView2 默认外部拖放（由自定义逻辑处理文件拖入）
        //AllowExternalDrop = false;

        this.SetVirtualHostNameToEmbeddedResourcesMapping();
        // 初始化托盘图标
        InitializeNotifyIcon();

        Load += MainWindow_Load;
        Icon = App.AppIcon;
        Shown += MainWindow_Shown;
    }

    private void MainWindow_Shown(object? sender, System.EventArgs e)
    {
        ;
    }

    private void MainWindow_Load(object? sender, System.EventArgs e)
    {
        CoreWebView2?.AddHostObjectToScript("cortana", new MainBridgeHostObject());
        SubscribeEvents();
        InstallDropHandler();
    }

    /// <summary>
    /// 订阅设置变更事件，通知前端刷新数据。
    /// </summary>
    private void SubscribeEvents()
    {
        _subscriber = App.Services.GetRequiredService<ISubscriber>();

        _subscriber.Subscribe<DataChangeArgs>(Events.OnAiProviderChange, (_, __) =>
        {
            Invoke(() => CoreWebView2?.ExecuteScriptAsync("typeof reloadProviders==='function'&&reloadProviders()"));
            return Task.FromResult(false);
        });

        _subscriber.Subscribe<DataChangeArgs>(Events.OnAiModelChange, (_, __) =>
        {
            Invoke(() => CoreWebView2?.ExecuteScriptAsync("typeof reloadModels==='function'&&reloadModels()"));
            return Task.FromResult(false);
        });
        _subscriber.On(Events.OnAgentChange, (_, _) =>
        {
            Invoke(() => CoreWebView2?.ExecuteScriptAsync("typeof reloadAgents==='function'&&reloadAgents()"));
            return Task.FromResult(false);
        });
    }

    /// <summary>
    /// 在 WebView2 中执行 JavaScript 脚本。供外部组件（如 COM 桥接对象）调用。
    /// </summary>
    internal void ExecuteJavaScript(string script)
    {
        Invoke(() => CoreWebView2?.ExecuteScriptAsync(script));
    }

    /// <summary>
    /// 初始化系统托盘图标和右键菜单。
    /// </summary>
    private void InitializeNotifyIcon()
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("显示界面", null, OnShowWindow);
        contextMenu.Items.Add("软件设置", null, OnOpenSettings);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("退出助理", null, OnExitApplication);

        _notifyIcon = new NotifyIcon
        {
            Icon = App.AppIcon,
            Text = "小娜宝贝 · AI 助手",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += OnShowWindow;
    }

    protected override void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // 用户点关闭按钮时隐藏窗口，不退出程序
        if (!_forceClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Visible = false;
        }
    }

    private void OnShowWindow(object? sender, System.EventArgs e)
    {
        Visible = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void OnOpenSettings(object? sender, System.EventArgs e)
    {
        var settings = App.Services.GetRequiredService<SettingsWindow>();
        settings.Show();
        settings.Activate();
    }

    private void OnExitApplication(object? sender, System.EventArgs e)
    {
        _subscriber?.Dispose();
        _forceClose = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Close();
    }

    // 【必须实现】配置窗口样式
    protected override WindowSettings ConfigureWindowSettings(
        HostWindowBuilder opts)
    {
        var settings = opts.UseDefaultWindow();
        settings.SystemBackdropType = SystemBackdropType.Acrylic;
        settings.ShowWindowDecorators = true;
        return settings;
    }

    // ──────── 拖放文件支持（Win32 窗口子类化） ────────

    private const int WM_DROPFILES = 0x0233;
    private const int GWLP_WNDPROC = -4;

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(nint hDrop, uint iFile, char[]? lpszFile, uint cch);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(nint hDrop);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(nint hWnd, bool fAccept);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    private nint _originalWndProc;
    private WndProcDelegate? _wndProcDelegate;

    /// <summary>
    /// 子类化窗口过程并注册接受拖放文件。在窗口加载完成后调用。
    /// </summary>
    private void InstallDropHandler()
    {
        nint hwnd = Handle;
        if (hwnd == nint.Zero) return;

        // 保持委托引用防止被 GC 回收
        _wndProcDelegate = DropWndProc;
        nint newProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
        _originalWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, newProc);

        // 注册窗口接受 WM_DROPFILES
        DragAcceptFiles(hwnd, true);
    }

    /// <summary>
    /// 自定义窗口过程，拦截 WM_DROPFILES 消息。
    /// </summary>
    private nint DropWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_DROPFILES)
        {
            HandleFileDrop(wParam);
            return nint.Zero;
        }

        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// 处理 WM_DROPFILES 消息，提取文件路径并通知前端。
    /// </summary>
    private void HandleFileDrop(nint hDrop)
    {
        try
        {
            uint fileCount = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
            if (fileCount == 0) return;

            var files = new List<object>((int)fileCount);

            for (uint i = 0; i < fileCount; i++)
            {
                uint size = DragQueryFile(hDrop, i, null, 0) + 1;
                char[] buffer = new char[size];
                DragQueryFile(hDrop, i, buffer, size);
                string path = new(buffer, 0, (int)size - 1);

                files.Add(new
                {
                    path,
                    name = Path.GetFileName(path),
                    type = MainBridgeHostObject.GetMimeType(path)
                });
            }

            string json = JsonSerializer.Serialize(files);
            string escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");
            ExecuteJavaScript($"typeof onFilesSelected==='function'&&onFilesSelected('{escaped}')");
        }
        finally
        {
            DragFinish(hDrop);
        }
    }
}