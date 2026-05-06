using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

using Netor.Cortana.Plugin;
using Netor.Cortana.Plugin.Mcp;

using System.Diagnostics;

namespace Netor.Cortana.UI.Views.Settings;

public partial class PluginManagementPage : UserControl
{
    private AgentService AgentService => App.Services.GetRequiredService<AgentService>();
    private GlobalPluginService GlobalPluginService => App.Services.GetRequiredService<GlobalPluginService>();
    private IAppPaths AppPaths => App.Services.GetRequiredService<IAppPaths>();
    private McpServerService McpServerService => App.Services.GetRequiredService<McpServerService>();
    private PluginLoader PluginLoader => App.Services.GetRequiredService<PluginLoader>();
    private IPublisher Publisher => App.Services.GetRequiredService<IPublisher>();

    private readonly List<LoadedPluginInfo> _plugins = [];
    private readonly List<McpServerEntity> _mcpServers = [];
    private readonly Dictionary<string, McpServerHost> _activeMcpHosts = new(StringComparer.OrdinalIgnoreCase);
    private LoadedPluginInfo? _selectedPlugin;
    private string? _selectedMcpServerId;

    public PluginManagementPage()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshPlugins();
    }

    private void RefreshPlugins()
    {
        _plugins.Clear();
        _plugins.AddRange(PluginLoader.GetLoadedPluginInfos().OrderBy(p => p.Plugin.Name, StringComparer.OrdinalIgnoreCase));

        _mcpServers.Clear();
        _mcpServers.AddRange(McpServerService.GetAll().OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase));

        _activeMcpHosts.Clear();
        foreach (var host in PluginLoader.GetActiveMcpServers())
        {
            _activeMcpHosts[host.Id] = host;
        }

        BuildPluginList();

        if (_selectedPlugin is not null)
        {
            _selectedPlugin = _plugins.FirstOrDefault(p => string.Equals(p.Plugin.Id, _selectedPlugin.Plugin.Id, StringComparison.OrdinalIgnoreCase));
            BuildDetails(_selectedPlugin);
        }
        else if (_selectedMcpServerId is not null)
        {
            BuildMcpDetails(_mcpServers.FirstOrDefault(m => string.Equals(m.Id, _selectedMcpServerId, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private void BuildPluginList()
    {
        PluginListPanel.Children.Clear();

        var search = TxtSearch.Text?.Trim() ?? string.Empty;
        var filtered = _plugins.Where(plugin => MatchesSearch(plugin, search)).ToList();
        var filteredMcpServers = _mcpServers.Where(server => MatchesSearch(server, search)).ToList();

        if (filtered.Count == 0 && filteredMcpServers.Count == 0)
        {
            PluginListPanel.Children.Add(new TextBlock
            {
            Text = "暂无匹配插件或 MCP 服务",
                Foreground = (IBrush)this.FindResource("SubtextBrush")!,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20)
            });
            return;
        }

        foreach (var plugin in filtered)
        {
            PluginListPanel.Children.Add(BuildPluginListItem(plugin));
        }

        foreach (var server in filteredMcpServers)
        {
            PluginListPanel.Children.Add(BuildMcpListItem(server));
        }
    }

    private Border BuildPluginListItem(LoadedPluginInfo pluginInfo)
    {
        var plugin = pluginInfo.Plugin;
        var isSelected = _selectedPlugin?.Plugin.Id == plugin.Id;
        var globalText = GlobalPluginService.IsEnabled(plugin.Id)
            ? " · 全局启用"
            : string.Empty;

        var title = new TextBlock
        {
            Text = plugin.Name,
            Foreground = (IBrush)this.FindResource("TextBrush")!,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var meta = new TextBlock
        {
            Text = $"v{plugin.Version} · 全局目录{globalText} · {plugin.Tools.Count} 个工具",
            Foreground = (IBrush)this.FindResource("SubtextBrush")!,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var desc = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(plugin.Description) ? "(无简介)" : plugin.Description,
            Foreground = (IBrush)this.FindResource("SubtextBrush")!,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(title);
        stack.Children.Add(meta);
        stack.Children.Add(desc);

        var border = new Border
        {
            Background = isSelected
                ? (IBrush)this.FindResource("Surface1Brush")!
                : (IBrush)this.FindResource("Surface0Brush")!,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8),
            Child = stack,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        border.PointerPressed += (_, _) =>
        {
            _selectedPlugin = pluginInfo;
            _selectedMcpServerId = null;
            BuildPluginList();
            BuildDetails(pluginInfo);
        };

        return border;
    }

    private Border BuildMcpListItem(McpServerEntity server)
    {
        var isSelected = string.Equals(_selectedMcpServerId, server.Id, StringComparison.OrdinalIgnoreCase);
        var isConnected = _activeMcpHosts.ContainsKey(server.Id);
        var statusText = server.IsEnabled
            ? isConnected ? "已连接" : "已启用 · 未连接"
            : "已禁用";

        var title = new TextBlock
        {
            Text = server.Name,
            Foreground = (IBrush)this.FindResource("TextBrush")!,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var meta = new TextBlock
        {
            Text = $"MCP 服务 · {server.TransportType} · {statusText}",
            Foreground = (IBrush)this.FindResource("SubtextBrush")!,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var desc = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(server.Description) ? "(无简介)" : server.Description,
            Foreground = (IBrush)this.FindResource("SubtextBrush")!,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(title);
        stack.Children.Add(meta);
        stack.Children.Add(desc);

        var border = new Border
        {
            Background = isSelected
                ? (IBrush)this.FindResource("Surface1Brush")!
                : (IBrush)this.FindResource("Surface0Brush")!,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8),
            Child = stack,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        border.PointerPressed += (_, _) =>
        {
            _selectedPlugin = null;
            _selectedMcpServerId = server.Id;
            BuildPluginList();
            BuildMcpDetails(server);
        };

        return border;
    }

    private void BuildDetails(LoadedPluginInfo? pluginInfo)
    {
        DetailPanel.Children.Clear();

        if (pluginInfo is null)
        {
            DetailPanel.Children.Add(new TextBlock
            {
                Text = "请选择一个插件",
                Foreground = (IBrush)this.FindResource("SubtextBrush")!,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40)
            });
            return;
        }

        var plugin = pluginInfo.Plugin;
        var isGlobalEnabled = GlobalPluginService.IsEnabled(plugin.Id);

        DetailPanel.Children.Add(new TextBlock
        {
            Text = plugin.Name,
            Foreground = (IBrush)this.FindResource("TextBrush")!,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold
        });

        DetailPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(plugin.Description) ? "(无简介)" : plugin.Description,
            Foreground = (IBrush)this.FindResource("SubtextBrush")!,
            TextWrapping = TextWrapping.Wrap
        });

        DetailPanel.Children.Add(BuildInfoText($"版本：v{plugin.Version}"));
        DetailPanel.Children.Add(BuildInfoText($"插件 ID：{plugin.Id}"));
        DetailPanel.Children.Add(BuildInfoText("来源：全局目录"));
        DetailPanel.Children.Add(BuildInfoText($"目录：{pluginInfo.DirectoryPath}"));
        DetailPanel.Children.Add(BuildInfoText($"工具数量：{plugin.Tools.Count}"));

        var globalToggle = new ToggleSwitch
        {
            OnContent = "全局插件已启用，所有智能体可用",
            OffContent = "设为全局插件",
            IsChecked = isGlobalEnabled,
            IsEnabled = true,
            Foreground = (IBrush)this.FindResource("TextBrush")!,
            FontWeight = FontWeight.Medium
        };
        globalToggle.IsCheckedChanged += (_, _) =>
        {
            if (_selectedPlugin is null) return;
            GlobalPluginService.SetEnabled(_selectedPlugin.Plugin.Id, globalToggle.IsChecked == true);
            Publisher.Publish(Events.OnPluginsChanged, new VoiceSignalArgs());
            BuildPluginList();
            BuildDetails(_selectedPlugin);
        };
        DetailPanel.Children.Add(globalToggle);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(BuildActionButton("卸载", OnUnloadClick, "btn-secondary"));
        buttons.Children.Add(BuildActionButton("重载", OnReloadClick, "btn-primary"));
        buttons.Children.Add(BuildActionButton("打开目录", OnOpenDirectoryClick, "btn-secondary"));
        buttons.Children.Add(BuildActionButton("删除插件", OnDeleteClick, "btn-danger"));
        DetailPanel.Children.Add(buttons);

        DetailPanel.Children.Add(BuildSectionHeader("工具列表"));
        if (plugin.Tools.Count == 0)
        {
            DetailPanel.Children.Add(BuildInfoText("该插件未提供工具。"));
        }
        else
        {
            DetailPanel.Children.Add(BuildInfoText(string.Join("，", plugin.Tools.Select(tool => tool.Name))));
        }

        if (isGlobalEnabled)
        {
            DetailPanel.Children.Add(BuildSectionHeader("绑定智能体"));
            DetailPanel.Children.Add(BuildInfoText("该插件已启用为全局插件，所有智能体默认可用，无需单独绑定。已有绑定会保留，关闭全局开关后仍可继续使用。"));
        }
        else
        {
            DetailPanel.Children.Add(BuildSectionHeader("绑定智能体"));
            DetailPanel.Children.Add(BuildInfoText("勾选后会立即保存当前插件和智能体的绑定关系。"));
            BuildAgentBindings(plugin.Id);
        }
    }

    private void BuildMcpDetails(McpServerEntity? server)
    {
        DetailPanel.Children.Clear();

        if (server is null)
        {
            DetailPanel.Children.Add(new TextBlock
            {
                Text = "请选择一个插件或 MCP 服务",
                Foreground = (IBrush)this.FindResource("SubtextBrush")!,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40)
            });
            return;
        }

        var isConnected = _activeMcpHosts.TryGetValue(server.Id, out var host);

        DetailPanel.Children.Add(new TextBlock
        {
            Text = server.Name,
            Foreground = (IBrush)this.FindResource("TextBrush")!,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold
        });

        DetailPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(server.Description) ? "(无简介)" : server.Description,
            Foreground = (IBrush)this.FindResource("SubtextBrush")!,
            TextWrapping = TextWrapping.Wrap
        });

        DetailPanel.Children.Add(BuildInfoText("类型：MCP 服务"));
        DetailPanel.Children.Add(BuildInfoText($"服务 ID：{server.Id}"));
        DetailPanel.Children.Add(BuildInfoText($"传输：{server.TransportType}"));
        DetailPanel.Children.Add(BuildInfoText($"状态：{(server.IsEnabled ? isConnected ? "已连接" : "已启用但未连接" : "已禁用")}"));
        DetailPanel.Children.Add(BuildInfoText($"地址：{GetMcpAddress(server)}"));
        DetailPanel.Children.Add(BuildInfoText($"工具数量：{(isConnected ? host!.Tools.Count : 0)}"));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(BuildActionButton("重连", OnReconnectMcpClick, "btn-primary"));
        DetailPanel.Children.Add(buttons);

        DetailPanel.Children.Add(BuildSectionHeader("工具列表"));
        if (!isConnected || host!.Tools.Count == 0)
        {
            DetailPanel.Children.Add(BuildInfoText("该 MCP 服务当前没有可用工具。"));
        }
        else
        {
            DetailPanel.Children.Add(BuildInfoText(string.Join("，", host.Tools.Select(tool => tool.Name))));
        }

        DetailPanel.Children.Add(BuildSectionHeader("绑定智能体"));
        DetailPanel.Children.Add(BuildInfoText("勾选后会立即保存当前 MCP 服务和智能体的绑定关系。"));
        BuildMcpAgentBindings(server.Id);
    }

    private void BuildAgentBindings(string pluginId)
    {
        var agents = AgentService.GetAll();
        if (agents.Count == 0)
        {
            DetailPanel.Children.Add(BuildInfoText("当前没有可用智能体。"));
            return;
        }

        var bindingsGrid = CreateTwoColumnGrid();

        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            var check = new CheckBox
            {
                Content = agent.Name,
                IsChecked = agent.EnabledPluginIds.Contains(pluginId, StringComparer.OrdinalIgnoreCase),
                Foreground = (IBrush)this.FindResource("TextBrush")!,
                Tag = agent.Id,
                VerticalAlignment = VerticalAlignment.Center
            };
            check.IsCheckedChanged += (_, _) => SaveAgentBinding(agent.Id, pluginId, check.IsChecked == true);

            AddTwoColumnGridChild(bindingsGrid, index, new Border
            {
                Background = (IBrush)this.FindResource("Surface1Brush")!,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6),
                Margin = GetTwoColumnItemMargin(index),
                Child = check
            });
        }

        DetailPanel.Children.Add(bindingsGrid);
    }

    private void BuildMcpAgentBindings(string serverId)
    {
        var agents = AgentService.GetAll();
        if (agents.Count == 0)
        {
            DetailPanel.Children.Add(BuildInfoText("当前没有可用智能体。"));
            return;
        }

        var bindingsGrid = CreateTwoColumnGrid();

        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            var check = new CheckBox
            {
                Content = agent.Name,
                IsChecked = agent.EnabledMcpServerIds.Contains(serverId, StringComparer.OrdinalIgnoreCase),
                Foreground = (IBrush)this.FindResource("TextBrush")!,
                Tag = agent.Id,
                VerticalAlignment = VerticalAlignment.Center
            };
            check.IsCheckedChanged += (_, _) => SaveMcpAgentBinding(agent.Id, serverId, check.IsChecked == true);

            AddTwoColumnGridChild(bindingsGrid, index, new Border
            {
                Background = (IBrush)this.FindResource("Surface1Brush")!,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6),
                Margin = GetTwoColumnItemMargin(index),
                Child = check
            });
        }

        DetailPanel.Children.Add(bindingsGrid);
    }

    private static Grid CreateTwoColumnGrid() => new()
    {
        ColumnDefinitions = new ColumnDefinitions("*,*"),
        RowDefinitions = new RowDefinitions(),
        Margin = new Thickness(0, 2, 0, 0)
    };

    private static void AddTwoColumnGridChild(Grid grid, int index, Control child)
    {
        var row = index / 2;
        var column = index % 2;

        while (grid.RowDefinitions.Count <= row)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        Grid.SetRow(child, row);
        Grid.SetColumn(child, column);
        grid.Children.Add(child);
    }

    private static Thickness GetTwoColumnItemMargin(int index)
    {
        return index % 2 == 0
            ? new Thickness(0, 0, 6, 8)
            : new Thickness(6, 0, 0, 8);
    }

    private TextBlock BuildInfoText(string text) => new()
    {
        Text = text,
        Foreground = (IBrush)this.FindResource("SubtextBrush")!,
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12
    };

    private TextBlock BuildSectionHeader(string text) => new()
    {
        Text = text,
        Foreground = (IBrush)this.FindResource("TextBrush")!,
        FontWeight = FontWeight.SemiBold,
        FontSize = 14,
        Margin = new Thickness(0, 8, 0, 0)
    };

    private Button BuildActionButton(string text, EventHandler<RoutedEventArgs> handler, string cssClass)
    {
        var button = new Button { Content = text };
        button.Classes.Add(cssClass);
        button.Click += handler;
        return button;
    }

    private void SaveAgentBinding(string agentId, string pluginId, bool shouldBind)
    {
        var agent = AgentService.GetById(agentId);
        if (agent is null) return;

        var hasBinding = agent.EnabledPluginIds.Contains(pluginId, StringComparer.OrdinalIgnoreCase);
        if (shouldBind == hasBinding) return;

        if (shouldBind)
        {
            agent.EnabledPluginIds = [.. agent.EnabledPluginIds, pluginId];
        }
        else
        {
            agent.EnabledPluginIds = [.. agent.EnabledPluginIds.Where(id => !string.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase))];
        }

        AgentService.Update(agent);
        Publisher.Publish(Events.OnAgentChange, new DataChangeArgs(agent.Id, ChangeType.Update));
    }

    private void SaveMcpAgentBinding(string agentId, string serverId, bool shouldBind)
    {
        var agent = AgentService.GetById(agentId);
        if (agent is null) return;

        var hasBinding = agent.EnabledMcpServerIds.Contains(serverId, StringComparer.OrdinalIgnoreCase);
        if (shouldBind == hasBinding) return;

        if (shouldBind)
        {
            agent.EnabledMcpServerIds = [.. agent.EnabledMcpServerIds, serverId];
        }
        else
        {
            agent.EnabledMcpServerIds = [.. agent.EnabledMcpServerIds.Where(id => !string.Equals(id, serverId, StringComparison.OrdinalIgnoreCase))];
        }

        AgentService.Update(agent);
        Publisher.Publish(Events.OnAgentChange, new DataChangeArgs(agent.Id, ChangeType.Update));
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => BuildPluginList();

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => RefreshPlugins();

    private void OnOpenGlobalDirectoryClick(object? sender, RoutedEventArgs e) => OpenDirectory(AppPaths.UserPluginsDirectory);

    private void OnOpenDirectoryClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedPlugin is null) return;
        OpenDirectory(_selectedPlugin.DirectoryPath);
    }

    private void OnUnloadClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedPlugin is null) return;
        PluginLoader.UnloadPlugin(_selectedPlugin.DirectoryName);
        RefreshPlugins();
    }

    private async void OnReloadClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedPlugin is null) return;
        await PluginLoader.ReloadPluginAsync(_selectedPlugin.DirectoryName);
        RefreshPlugins();
    }

    private async void OnReconnectMcpClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedMcpServerId is null) return;
        await PluginLoader.ReconnectMcpAsync(_selectedMcpServerId, McpServerService);
        RefreshPlugins();
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedPlugin is null) return;

        var pluginInfo = _selectedPlugin;
        PluginLoader.UnloadPlugin(pluginInfo.DirectoryName);
        TryDeletePluginDirectory(pluginInfo.DirectoryPath);
        GlobalPluginService.Remove(pluginInfo.Plugin.Id);
        RemovePluginBindingFromAllAgents(pluginInfo.Plugin.Id);
        _selectedPlugin = null;
        RefreshPlugins();
    }

    private void RemovePluginBindingFromAllAgents(string pluginId)
    {
        foreach (var agent in AgentService.GetAll())
        {
            if (!agent.EnabledPluginIds.Contains(pluginId, StringComparer.OrdinalIgnoreCase)) continue;

            agent.EnabledPluginIds = [.. agent.EnabledPluginIds.Where(id => !string.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase))];
            AgentService.Update(agent);
            Publisher.Publish(Events.OnAgentChange, new DataChangeArgs(agent.Id, ChangeType.Update));
        }
    }

    private void TryDeletePluginDirectory(string directoryPath)
    {
        if (!IsPluginDirectory(directoryPath)) return;
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive: true);
    }

    private bool IsPluginDirectory(string directoryPath)
    {
        var fullPath = NormalizeDirectory(directoryPath);
        var userRoot = NormalizeDirectory(AppPaths.UserPluginsDirectory);

        return fullPath.StartsWith(userRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectory(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }

    private static bool MatchesSearch(LoadedPluginInfo pluginInfo, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;

        var plugin = pluginInfo.Plugin;
        return plugin.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || plugin.Id.Contains(search, StringComparison.OrdinalIgnoreCase)
            || plugin.Description.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSearch(McpServerEntity server, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;

        return server.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || server.Id.Contains(search, StringComparison.OrdinalIgnoreCase)
            || server.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
            || server.TransportType.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetMcpAddress(McpServerEntity server)
    {
        return server.TransportType == "stdio"
            ? string.Join(" ", new[] { server.Command }.Concat(server.Arguments).Where(value => !string.IsNullOrWhiteSpace(value)))
            : server.Url;
    }

    private static void OpenDirectory(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
        Process.Start(new ProcessStartInfo(directoryPath) { UseShellExecute = true });
    }
}
