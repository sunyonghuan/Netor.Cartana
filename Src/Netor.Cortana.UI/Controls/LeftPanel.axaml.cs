using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

using Netor.Cortana.UI.ViewModels;

namespace Netor.Cortana.UI.Controls;

/// <summary>
/// 左侧面板用户控件（界面重设计 C3，决策 UI-1 L2 + UI-9 底部 tab）。
///
/// 容器结构（Grid Rows = 36 / * / 32）：
/// - Row 0：标题栏占位（替代原 WorkspaceExplorer 内部的 36px hack）
/// - Row 1：Tab 内容区（Tab1 = WorkspaceExplorer，Tab2 = EmptyState 占位）
/// - Row 2：底部 Tab 切换栏（VS 经典风格，active 蓝字 + 加粗）
///
/// 对外 API（转发自内部 <see cref="WorkspaceExplorer"/>）：
/// - <see cref="WorkspaceDirectory"/>：当前工作目录路径
/// - <see cref="AttachmentRequested"/>：用户在文件树右键 → 引用为附件
///
/// DataContext 约定：调用方（MainWindow）必须在 Loaded 前设置 <c>DataContext = LeftPanelVm</c>，
/// 否则 axaml 的 Binding 不生效。本控件不在构造函数注入 VM（避免与 axaml InitializeComponent
/// 时序耦合 + 与现有 DI 风格保持一致）。
///
/// 详见 Docs/未来版本策划/界面重设计/01-布局规格.md §3 + 04-实施阶段.md §3。
/// </summary>
public partial class LeftPanel : UserControl
{
    /// <summary>
    /// 已绑定的 VM 引用（在 DataContext 变化时缓存，便于 click handler 直接读写）。
    /// </summary>
    private LeftPanelVm? _vm;

    /// <summary>
    /// 初始化左侧面板。InitializeComponent 完成后注册 DataContextChanged 监听，
    /// 用户 Click 事件则直接操作 _vm.ActiveTabIndex。
    /// </summary>
    public LeftPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // 内部 WorkspaceExplorer 的事件转发到本控件对外暴露的 event
        FileExplorerHost.AttachmentRequested += paths => AttachmentRequested?.Invoke(paths);
    }

    /// <summary>
    /// 当前工作目录路径。转发到内部 <see cref="WorkspaceExplorer"/>。
    /// </summary>
    public string WorkspaceDirectory
    {
        get => FileExplorerHost.WorkspaceDirectory;
        set => FileExplorerHost.WorkspaceDirectory = value;
    }

    /// <summary>
    /// 用户在文件树右键选择 "引用为附件" 时触发。转发自内部 <see cref="WorkspaceExplorer"/>。
    /// </summary>
    public event Action<IReadOnlyList<string>>? AttachmentRequested;

    /// <summary>
    /// DataContext 变化时重新绑定 VM 监听 + 同步 active 样式。
    /// </summary>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // 解除旧 VM 的事件监听
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as LeftPanelVm;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            ApplyActiveTabClass(_vm.ActiveTabIndex);
        }
    }

    /// <summary>
    /// VM 属性变化时同步底部 Tab 按钮的 active 样式。
    /// </summary>
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LeftPanelVm.ActiveTabIndex) && _vm is not null)
        {
            ApplyActiveTabClass(_vm.ActiveTabIndex);
        }
    }

    /// <summary>
    /// 根据 ActiveTabIndex 给底部 Tab1Button / Tab2Button 加 / 去 "left-tab-active" CSS 类。
    /// </summary>
    private void ApplyActiveTabClass(int index)
    {
        var tab1Active = index == 0;
        Tab1Button.Classes.Set("left-tab-active", tab1Active);
        Tab2Button.Classes.Set("left-tab-active", !tab1Active);
    }

    /// <summary>
    /// 点击 Tab1（文件目录）：切到 Tab1。
    /// </summary>
    private void OnTab1Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.ActiveTabIndex = 0;
    }

    /// <summary>
    /// 点击 Tab2（动态 tab）：切到 Tab2。
    /// </summary>
    private void OnTab2Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.ActiveTabIndex = 1;
    }
}
