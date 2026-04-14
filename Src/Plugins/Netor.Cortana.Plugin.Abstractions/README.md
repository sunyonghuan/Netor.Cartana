# Netor.Cortana.Plugin.Abstractions

Cortana 插件系统的**抽象接口库**，定义了插件开发的核心契约。所有插件（托管插件和原生插件）都基于此库定义的接口进行交互。

## 功能

- **`IPlugin`** — 插件核心接口，声明 Id、名称、版本、工具列表、AI 指令等
- **`IPluginContext`** — 宿主向插件暴露的有限上下文，提供数据目录、日志、HttpClient 等基础设施

## 安装

```shell
dotnet add package Netor.Cortana.Plugin.Abstractions
```

## 核心接口

### IPlugin

插件必须实现的主接口，宿主通过此接口加载和管理插件。

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

| 属性 | 说明 |
|------|------|
| `Id` | 插件唯一标识 |
| `Name` | 插件名称 |
| `Version` | 插件版本 |
| `Description` | 插件描述 |
| `Tags` | 分类标签 |
| `Tools` | 插件提供的 AI 工具列表（基于 `Microsoft.Extensions.AI`） |
| `Instructions` | AI 系统指令片段，告诉 AI 何时及如何使用这些工具 |
| `InitializeAsync` | 插件初始化入口，宿主传入 `IPluginContext` |

### IPluginContext

宿主向插件暴露的有限上下文，避免插件直接访问宿主内部。

```csharp
public interface IPluginContext
{
    string DataDirectory { get; }
    string WorkspaceDirectory { get; }
    ILoggerFactory LoggerFactory { get; }
    IHttpClientFactory HttpClientFactory { get; }
    int WsPort { get; }
}
```

| 属性 | 说明 |
|------|------|
| `DataDirectory` | 插件专属的数据存储目录 |
| `WorkspaceDirectory` | 当前工作区目录 |
| `LoggerFactory` | 宿主提供的日志工厂 |
| `HttpClientFactory` | 宿主提供的 HttpClientFactory |
| `WsPort` | WebSocket 服务器端口，供插件建立 WS 连接 |

## 依赖

- `Microsoft.Extensions.AI.Abstractions` — AI 工具抽象（`AITool`）
- `Microsoft.Extensions.Http` — `IHttpClientFactory`
- `Microsoft.Extensions.Logging.Abstractions` — `ILoggerFactory`

## 要求

- .NET Standard 2.0+（兼容 .NET Framework 4.6.1+ 和 .NET Core 2.0+）
