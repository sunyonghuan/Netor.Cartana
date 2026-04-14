# Dotnet 托管插件开发指南

> 状态：历史兼容文档。当前项目的新插件开发默认推荐 Native 或 MCP；仅当你明确需要维护现有 IPlugin 插件时，再继续使用 Dotnet 通道。

## 当前定位

Dotnet 通道仍然存在于运行时中，能够继续加载历史托管插件，但它已经不是当前推荐的插件主路线。

优先选择：

- Native：本地高性能、AOT、强隔离
- MCP：远程服务接入、跨语言工具集成

继续使用 Dotnet 的典型场景：

- 维护已有的 IPlugin 插件
- 短期内无法迁移到 Native 或 MCP 的内部工具
- 强依赖 IPluginContext 且迁移成本暂时不可接受的存量能力

## 通道说明

| 项目 | 内容 |
|------|------|
| runtime | dotnet |
| 加载方式 | AssemblyLoadContext |
| 当前状态 | 兼容保留 |
| 新项目建议 | 不建议作为默认方案 |

## 最小结构

```text
.cortana/plugins/my-plugin/
  plugin.json
  MyPlugin.dll
  其他依赖 DLL
```

```json
{
  "id": "com.example.my-plugin",
  "name": "我的插件",
  "version": "1.0.0",
  "runtime": "dotnet",
  "assemblyName": "MyPlugin.dll",
  "minHostVersion": "1.0.0"
}
```

## IPlugin 契约

Dotnet 插件仍然基于 Netor.Cortana.Plugin.Abstractions 中的 IPlugin 和 IPluginContext：

```csharp
public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    Version Version { get; }
    string Description { get; }
    IReadOnlyList<string> Tags { get; }
    IReadOnlyList<AITool> Tools { get; }
    string? Instructions { get; }
    Task InitializeAsync(IPluginContext context);
}
```

## 维护建议

- 不要再为新需求优先创建新的 Dotnet 插件。
- 若只是为了本地工具扩展，优先迁移到 Native。
- 若能力本身属于独立服务，优先迁移到 MCP。
- 对存量 Dotnet 插件，只做必要修复、兼容和迁移收口。

## 迁移建议

### 迁移到 Native

适合：本地执行、性能敏感、需要 AOT、安全隔离的工具。

参考文档：Docs/plugin-native.md

### 迁移到 MCP

适合：外部服务、跨语言、远程部署、共享工具服务。

参考文档：Docs/plugin-mcp.md

## 结论

Dotnet 通道没有被仓库完全删除，但它已经不再代表项目的默认扩展方向。阅读本文件时，应把它视为历史兼容文档，而不是新插件开发模板。