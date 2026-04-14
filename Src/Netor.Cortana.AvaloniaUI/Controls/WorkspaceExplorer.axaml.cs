using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Netor.Cortana.AvaloniaUI.Controls;

public partial class WorkspaceExplorer : UserControl
{
    private static readonly IBrush DragBorderBrush = SolidColorBrush.Parse("#007ACC");
    private static readonly IBrush DragBackgroundBrush = SolidColorBrush.Parse("#1a007ACC");

    private static readonly Bitmap FolderIcon = new(AssetLoader.Open(new Uri("avares://Cortana/Assets/folder.png")));
    private static readonly Bitmap FileIcon = new(AssetLoader.Open(new Uri("avares://Cortana/Assets/file.png")));

    private FileSystemWatcher? _watcher;
    private string _workspaceDirectory = string.Empty;
    private readonly List<string> _clipboardPaths = [];
    private TreeViewItem? _activeDropTargetItem;
    private bool _isRootDropTarget;

    // WorkspaceChanged 事件已迁移到 EventHub（Events.OnWorkspaceChanged）

    /// <summary>
    /// 请求将文件路径列表添加为聊天附件，由 MainWindow 订阅。
    /// </summary>
    public event Action<IReadOnlyList<string>>? AttachmentRequested;

    public WorkspaceExplorer()
    {
        InitializeComponent();
        FileTree.AddHandler(DragDrop.DragEnterEvent, OnTreeDragEnter);
        FileTree.AddHandler(DragDrop.DragOverEvent, OnTreeDragOver);
        FileTree.AddHandler(DragDrop.DragLeaveEvent, OnTreeDragLeave);
        FileTree.AddHandler(DragDrop.DropEvent, OnTreeDrop);
    }

    public ObservableCollection<FileTreeNode> TreeNodes { get; } = [];

    public string WorkspaceDirectory
    {
        get => _workspaceDirectory;
        set
        {
            // 统一去掉尾部分隔符，避免 Uri.LocalPath 产生的尾 \ 导致路径比较失败
            var normalized = value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(_workspaceDirectory, normalized, StringComparison.OrdinalIgnoreCase))
                return;
            _workspaceDirectory = normalized;
            LoadTree();
        }
    }

    // ──────── 文件树构建 ────────

    private void LoadTree()
    {
        TreeNodes.Clear();
        DisposeWatcher();

        if (string.IsNullOrEmpty(_workspaceDirectory) || !Directory.Exists(_workspaceDirectory))
            return;

        foreach (var node in ScanDirectory(_workspaceDirectory))
            TreeNodes.Add(node);

        FileTree.ItemsSource = TreeNodes;
        StartWatcher();
    }

    private static List<FileTreeNode> ScanDirectory(string path)
    {
        var result = new List<FileTreeNode>();

        try
        {
            var dirInfo = new DirectoryInfo(path);

            // 文件夹在前，按名称排序；过滤 . 开头的隐藏目录
            var dirs = dirInfo.GetDirectories()
                .Where(d => !d.Name.StartsWith('.'))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var dir in dirs)
            {
                var node = new FileTreeNode
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true,
                    Icon = FolderIcon,
                    Children = new ObservableCollection<FileTreeNode>(ScanDirectory(dir.FullName))
                };
                result.Add(node);
            }

            var files = dirInfo.GetFiles()
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                result.Add(new FileTreeNode
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false,
                    Icon = FileIcon
                });
            }
        }
        catch
        {
            // 权限不足等情况静默忽略
        }

        return result;
    }

    // ──────── FileSystemWatcher ────────

    private void StartWatcher()
    {
        if (string.IsNullOrEmpty(_workspaceDirectory) || !Directory.Exists(_workspaceDirectory))
            return;

        _watcher = new FileSystemWatcher(_workspaceDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileSystemChanged;
        _watcher.Deleted += OnFileSystemChanged;
        _watcher.Renamed += OnFileSystemChanged;
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        // 忽略 . 开头的隐藏目录内变更
        var relative = Path.GetRelativePath(_workspaceDirectory, e.FullPath);
        if (relative.StartsWith('.'))
            return;

        Dispatcher.UIThread.Post(() => RefreshTreeByChange(e));
    }

    private void RefreshTreeByChange(FileSystemEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_workspaceDirectory) || !Directory.Exists(_workspaceDirectory))
            return;

        // 文件/目录新增或删除时，刷新其父目录；重命名时，刷新旧父目录和新父目录。
        if (e is RenamedEventArgs renamed)
        {
            RefreshDirectory(Path.GetDirectoryName(renamed.OldFullPath));
            RefreshDirectory(Path.GetDirectoryName(renamed.FullPath));
            return;
        }

        RefreshDirectory(Path.GetDirectoryName(e.FullPath));
    }

    private void RefreshDirectory(string? targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            return;

        // 记住当前选中节点
        var selectedPath = (FileTree.SelectedItem as FileTreeNode)?.FullPath;

        // 统一去掉尾部分隔符再比较
        var normalizedTarget = targetDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(normalizedTarget, _workspaceDirectory, StringComparison.OrdinalIgnoreCase))
        {
            var rootExpandedState = TreeNodes
                .Where(t => t.IsDirectory)
                .ToDictionary(t => t.FullPath, t => t.IsExpanded, StringComparer.OrdinalIgnoreCase);

            TreeNodes.Clear();
            foreach (var node in ScanDirectory(_workspaceDirectory))
            {
                if (node.IsDirectory && rootExpandedState.TryGetValue(node.FullPath, out var expanded))
                    node.IsExpanded = expanded;

                TreeNodes.Add(node);
            }

            RestoreSelection(selectedPath);
            return;
        }

        var dirNode = FindNodeByPath(TreeNodes, normalizedTarget);
        if (dirNode is null || !dirNode.IsDirectory)
            return;

        var childExpandedState = dirNode.Children
            .Where(t => t.IsDirectory)
            .ToDictionary(t => t.FullPath, t => t.IsExpanded, StringComparer.OrdinalIgnoreCase);

        var children = ScanDirectory(normalizedTarget);

        dirNode.Children.Clear();
        foreach (var child in children)
        {
            if (child.IsDirectory && childExpandedState.TryGetValue(child.FullPath, out var expanded))
                child.IsExpanded = expanded;

            dirNode.Children.Add(child);
        }

        RestoreSelection(selectedPath);
    }

    private void RestoreSelection(string? selectedPath)
    {
        if (selectedPath is null) return;
        var target = FindNodeByPath(TreeNodes, selectedPath);
        if (target is not null)
            FileTree.SelectedItem = target;
    }

    private static FileTreeNode? FindNodeByPath(IEnumerable<FileTreeNode> nodes, string fullPath)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
                return node;

            if (!node.IsDirectory || node.Children.Count == 0)
                continue;

            var found = FindNodeByPath(node.Children, fullPath);
            if (found is not null)
                return found;
        }

        return null;
    }

    private void DisposeWatcher()
    {
        if (_watcher is null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnFileSystemChanged;
        _watcher.Deleted -= OnFileSystemChanged;
        _watcher.Renamed -= OnFileSystemChanged;
        _watcher.Dispose();
        _watcher = null;
    }

    // ──────── 工具栏事件 ────────

    private async void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择工作目录",
            AllowMultiple = false
        });

        if (result.Count == 0) return;

        var newPath = result[0].Path.LocalPath;

        // 通过 EventHub 广播，所有订阅者自动同步
        var publisher = App.Services.GetRequiredService<IPublisher>();
        publisher.Publish(Events.OnWorkspaceChanged, new WorkspaceChangedArgs(newPath));
    }

    private async void OnNewFileClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_workspaceDirectory) || !Directory.Exists(_workspaceDirectory))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var result = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "新建文件",
            SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(_workspaceDirectory),
            SuggestedFileName = "untitled.txt"
        });

        if (result is null) return;

        var filePath = result.Path.LocalPath;
        if (!File.Exists(filePath))
            await File.WriteAllTextAsync(filePath, string.Empty);
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        LoadTree();
    }

    private void OnCollapseAllClick(object? sender, RoutedEventArgs e)
    {
        CollapseAll(TreeNodes);
    }

    private static void CollapseAll(IEnumerable<FileTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = false;
            if (node.Children.Count > 0)
                CollapseAll(node.Children);
        }
    }

    // ──────── 文件双击打开 ────────

    private void OnTreeDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileTreeNode node || node.IsDirectory)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(node.FullPath) { UseShellExecute = true });
        }
        catch
        {
            // 无关联程序等情况静默忽略
        }
    }

    // ──────── 右键菜单 ────────

    /// <summary>
    /// 获取当前选中的所有节点（多选支持）。
    /// </summary>
    private List<FileTreeNode> GetSelectedNodes()
    {
        var nodes = new List<FileTreeNode>();
        if (FileTree.SelectedItems is null) return nodes;

        foreach (var item in FileTree.SelectedItems)
        {
            if (item is FileTreeNode node)
                nodes.Add(node);
        }
        return nodes;
    }

    /// <summary>
    /// 右键菜单打开前，根据选中节点类型控制菜单项可见性。
    /// </summary>
    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var nodes = GetSelectedNodes();
        if (nodes.Count == 0)
        {
            e.Cancel = true;
            return;
        }

        var isSingle = nodes.Count == 1;
        var singleNode = isSingle ? nodes[0] : null;
        var hasFile = nodes.Any(n => !n.IsDirectory);
        var hasDir = nodes.Any(n => n.IsDirectory);
        var allFiles = nodes.All(n => !n.IsDirectory);

        // 打开：仅单选文件
        MenuOpen.IsVisible = isSingle && !singleNode!.IsDirectory;
        // 在资源管理器中显示：任意选择
        MenuOpenInExplorer.IsVisible = true;
        // 在终端中打开：仅单选文件夹
        MenuOpenInTerminal.IsVisible = isSingle && singleNode!.IsDirectory;
        // 发送到聊天附件：有文件时可用
        MenuSendToChat.IsVisible = hasFile;
        // 新建文件/文件夹：仅单选文件夹
        MenuNewFile.IsVisible = isSingle && singleNode!.IsDirectory;
        MenuNewFolder.IsVisible = isSingle && singleNode!.IsDirectory;
        // 复制文件：任意选择
        MenuCopy.IsVisible = true;
        // 粘贴：仅单选文件夹 + 剪贴板有内容
        MenuPaste.IsVisible = isSingle && singleNode!.IsDirectory && _clipboardPaths.Count > 0;
        // 重命名：仅单选
        MenuRename.IsVisible = isSingle;
        // 删除：任意选择
        MenuDelete.IsVisible = true;
        // 复制路径：任意选择
        MenuCopyPath.IsVisible = true;
        MenuCopyRelPath.IsVisible = true;
    }

    private void OnMenuOpenClick(object? sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileTreeNode node || node.IsDirectory) return;
        try { Process.Start(new ProcessStartInfo(node.FullPath) { UseShellExecute = true }); } catch { }
    }

    private void OnMenuOpenInExplorerClick(object? sender, RoutedEventArgs e)
    {
        foreach (var node in GetSelectedNodes())
        {
            try
            {
                if (node.IsDirectory)
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{node.FullPath}\"") { UseShellExecute = true });
                else
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{node.FullPath}\"") { UseShellExecute = true });
            }
            catch { }
        }
    }

    private void OnMenuOpenInTerminalClick(object? sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileTreeNode { IsDirectory: true } node) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"-NoExit -Command \"Set-Location '{node.FullPath}'\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void OnMenuSendToChatClick(object? sender, RoutedEventArgs e)
    {
        var filePaths = GetSelectedNodes()
            .Where(n => !n.IsDirectory)
            .Select(n => n.FullPath)
            .ToList();

        if (filePaths.Count > 0)
            AttachmentRequested?.Invoke(filePaths);
    }

    private async void OnMenuNewFileClick(object? sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileTreeNode { IsDirectory: true } dirNode) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var result = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "新建文件",
            SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(dirNode.FullPath),
            SuggestedFileName = "untitled.txt"
        });

        if (result is null) return;
        var filePath = result.Path.LocalPath;
        if (!File.Exists(filePath))
            await File.WriteAllTextAsync(filePath, string.Empty);
    }

    private void OnMenuNewFolderClick(object? sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileTreeNode { IsDirectory: true } dirNode) return;

        var baseName = "新建文件夹";
        var targetPath = Path.Combine(dirNode.FullPath, baseName);
        var counter = 1;
        while (Directory.Exists(targetPath))
        {
            targetPath = Path.Combine(dirNode.FullPath, $"{baseName} ({counter++})");
        }

        try
        {
            Directory.CreateDirectory(targetPath);
        }
        catch { }
    }

    private void OnMenuCopyClick(object? sender, RoutedEventArgs e)
    {
        _clipboardPaths.Clear();
        _clipboardPaths.AddRange(GetSelectedNodes().Select(n => n.FullPath));
    }

    private void OnMenuPasteClick(object? sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileTreeNode { IsDirectory: true } dirNode) return;
        if (_clipboardPaths.Count == 0) return;

        CopyPathsToDirectory(_clipboardPaths, dirNode.FullPath);
    }

    private void CopyPathsToDirectory(IEnumerable<string> sourcePaths, string targetDirectory)
    {
        foreach (var sourcePath in sourcePaths)
        {
            try
            {
                var name = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(targetDirectory, name);

                // 目标已存在时自动加序号
                if (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    var nameOnly = Path.GetFileNameWithoutExtension(name);
                    var ext = Path.GetExtension(name);
                    var counter = 1;
                    do
                    {
                        destPath = Path.Combine(targetDirectory, $"{nameOnly} - 副本{(counter > 1 ? $" ({counter})" : "")}{ext}");
                        counter++;
                    } while (File.Exists(destPath) || Directory.Exists(destPath));
                }

                if (File.Exists(sourcePath))
                    File.Copy(sourcePath, destPath);
                else if (Directory.Exists(sourcePath))
                    CopyDirectoryRecursive(sourcePath, destPath);
            }
            catch { }
        }
    }

    private void OnTreeDragEnter(object? sender, DragEventArgs e)
    {
        UpdateDropVisual(e);
        e.Handled = true;
    }

    private void OnTreeDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.None;
            ClearDropVisual();
            e.Handled = true;
            return;
        }

        e.DragEffects = TryResolveDropDirectory(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        UpdateDropVisual(e);
        e.Handled = true;
    }

    private void OnTreeDragLeave(object? sender, RoutedEventArgs e)
    {
        ClearDropVisual();
    }

    private async void OnTreeDrop(object? sender, DragEventArgs e)
    {
        try
        {
            if (!TryResolveDropDirectory(e, out var targetDirectory) || string.IsNullOrWhiteSpace(targetDirectory))
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }

            var items = e.DataTransfer.TryGetFiles();
            if (items is null)
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }

            var sourcePaths = items
                .Select(i => i.Path?.LocalPath)
                .Where(p => !string.IsNullOrWhiteSpace(p) && (File.Exists(p) || Directory.Exists(p)))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sourcePaths.Count == 0)
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }

            await Task.Run(() => CopyPathsToDirectory(sourcePaths, targetDirectory));
            e.DragEffects = DragDropEffects.Copy;
        }
        finally
        {
            ClearDropVisual();
            e.Handled = true;
        }
    }

    private bool TryResolveDropDirectory(DragEventArgs e, out string targetDirectory)
    {
        targetDirectory = string.Empty;

        if (!e.DataTransfer.Contains(DataFormat.File))
            return false;

        var targetNode = ResolveTargetNodeFromDragEvent(e);
        if (targetNode is null)
        {
            if (string.IsNullOrWhiteSpace(_workspaceDirectory) || !Directory.Exists(_workspaceDirectory))
                return false;

            targetDirectory = _workspaceDirectory;
            return true;
        }

        if (targetNode.IsDirectory)
        {
            targetDirectory = targetNode.FullPath;
            return Directory.Exists(targetDirectory);
        }

        var parentDirectory = Path.GetDirectoryName(targetNode.FullPath);
        if (string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
            return false;

        targetDirectory = parentDirectory;
        return true;
    }

    private FileTreeNode? ResolveTargetNodeFromDragEvent(DragEventArgs e)
    {
        var sourceVisual = e.Source as Visual;
        if (sourceVisual is null)
            return null;

        var item = sourceVisual.FindAncestorOfType<TreeViewItem>();
        return item?.DataContext as FileTreeNode;
    }

    private void UpdateDropVisual(DragEventArgs e)
    {
        var sourceVisual = e.Source as Visual;
        var currentItem = sourceVisual?.FindAncestorOfType<TreeViewItem>();

        if (!e.DataTransfer.Contains(DataFormat.File))
        {
            ClearDropVisual();
            return;
        }

        if (currentItem is not null && currentItem.DataContext is FileTreeNode)
        {
            if (!ReferenceEquals(_activeDropTargetItem, currentItem))
            {
                _activeDropTargetItem?.Classes.Remove("drop-target");
                _activeDropTargetItem = currentItem;
                _activeDropTargetItem.Classes.Add("drop-target");
            }

            if (_isRootDropTarget)
            {
                _isRootDropTarget = false;
                FileTree.BorderBrush = Brushes.Transparent;
                FileTree.Background = Brushes.Transparent;
            }

            return;
        }

        _activeDropTargetItem?.Classes.Remove("drop-target");
        _activeDropTargetItem = null;

        if (!_isRootDropTarget)
        {
            _isRootDropTarget = true;
            FileTree.BorderBrush = DragBorderBrush;
            FileTree.Background = DragBackgroundBrush;
        }
    }

    private void ClearDropVisual()
    {
        _activeDropTargetItem?.Classes.Remove("drop-target");
        _activeDropTargetItem = null;

        if (_isRootDropTarget)
        {
            _isRootDropTarget = false;
            FileTree.BorderBrush = Brushes.Transparent;
            FileTree.Background = Brushes.Transparent;
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)));

        foreach (var dir in Directory.GetDirectories(sourceDir))
            CopyDirectoryRecursive(dir, Path.Combine(destDir, Path.GetFileName(dir)));
    }

    private async void OnMenuRenameClick(object? sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileTreeNode node) return;

        // 使用简单的输入对话框（通过 Window 弹出）
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window window) return;

        var inputBox = new TextBox
        {
            Text = node.Name,
            SelectionStart = 0,
            SelectionEnd = node.IsDirectory
                ? node.Name.Length
                : Path.GetFileNameWithoutExtension(node.Name).Length,
            MinWidth = 280,
            FontSize = 13,
        };

        var dialog = new Window
        {
            Title = "重命名",
            Width = 340,
            Height = 130,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 12,
                Children =
                {
                    inputBox,
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            new Button { Content = "取消", Tag = "cancel", MinWidth = 70 },
                            new Button { Content = "确定", Tag = "ok", MinWidth = 70 },
                        }
                    }
                }
            }
        };

        var confirmed = false;
        foreach (var btn in ((StackPanel)((StackPanel)dialog.Content).Children[1]).Children.OfType<Button>())
        {
            btn.Click += (_, _) =>
            {
                confirmed = btn.Tag as string == "ok";
                dialog.Close();
            };
        }

        inputBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter) { confirmed = true; dialog.Close(); }
            if (ke.Key == Key.Escape) { dialog.Close(); }
        };

        await dialog.ShowDialog(window);

        if (!confirmed) return;
        var newName = inputBox.Text?.Trim();
        if (string.IsNullOrEmpty(newName) || newName == node.Name) return;

        try
        {
            var parentDir = Path.GetDirectoryName(node.FullPath)!;
            var newPath = Path.Combine(parentDir, newName);

            if (node.IsDirectory)
                Directory.Move(node.FullPath, newPath);
            else
                File.Move(node.FullPath, newPath);
        }
        catch { }
    }

    private async void OnMenuDeleteClick(object? sender, RoutedEventArgs e)
    {
        var nodes = GetSelectedNodes();
        if (nodes.Count == 0) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window window) return;

        var message = nodes.Count == 1
            ? $"确定要删除 \"{nodes[0].Name}\" 吗？"
            : $"确定要删除选中的 {nodes.Count} 个项目吗？";

        if (nodes.Any(n => n.IsDirectory))
            message += "\n\n文件夹将被递归删除，此操作不可撤销。";

        var confirmed = false;
        var dialog = new Window
        {
            Title = "确认删除",
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 13 },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            new Button { Content = "取消", Tag = "cancel", MinWidth = 70 },
                            new Button { Content = "删除", Tag = "ok", MinWidth = 70, Foreground = Avalonia.Media.SolidColorBrush.Parse("#f14c4c") },
                        }
                    }
                }
            }
        };

        foreach (var btn in ((StackPanel)((StackPanel)dialog.Content).Children[1]).Children.OfType<Button>())
        {
            btn.Click += (_, _) =>
            {
                confirmed = btn.Tag as string == "ok";
                dialog.Close();
            };
        }

        await dialog.ShowDialog(window);

        if (!confirmed) return;

        foreach (var node in nodes)
        {
            try
            {
                if (node.IsDirectory)
                    Directory.Delete(node.FullPath, recursive: true);
                else
                    File.Delete(node.FullPath);
            }
            catch { }
        }
    }

    private async void OnMenuCopyPathClick(object? sender, RoutedEventArgs e)
    {
        var paths = string.Join(Environment.NewLine, GetSelectedNodes().Select(n => n.FullPath));
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is not null)
            await topLevel.Clipboard.SetTextAsync(paths);
    }

    private async void OnMenuCopyRelPathClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_workspaceDirectory)) return;
        var paths = string.Join(Environment.NewLine,
            GetSelectedNodes().Select(n => Path.GetRelativePath(_workspaceDirectory, n.FullPath)));
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is not null)
            await topLevel.Clipboard.SetTextAsync(paths);
    }

    // ──────── 清理 ────────

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        ClearDropVisual();
        DisposeWatcher();
        base.OnUnloaded(e);
    }
}

/// <summary>
/// 文件树节点。
/// </summary>
public sealed class FileTreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;

    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required bool IsDirectory { get; init; }
    public required Bitmap Icon { get; init; }
    public ObservableCollection<FileTreeNode> Children { get; set; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
