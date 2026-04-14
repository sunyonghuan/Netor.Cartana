# Netor.Cortana 插件系统设计文档

> 版本：1.0.0 | 日期：2025-07  
> 状态：设计阶段

> 历史说明：本文档基于旧阶段的插件设计讨论，包含 process、旧 Dotnet 主路线等方案。当前仓库以 Native 和 MCP 为主，阅读时请以根目录 Docs 中的最新文档为准。

---

## 一、架构总览

### 1.1 目标

为 Netor.Cortana AI 助手提供可扩展的插件系统，允许第三方开发者为 AI 添加新的工具能力。

核心需求：

- **热插拔** — 不重启应用即可加载/卸载插件
- **多语言支持** — 不限于 C#，支持任意编程语言编写插件
- **安全隔离** — 单个插件故障不影响宿主和其他插件
- **接口轻量** — 插件开发者只需引用极小的接口包

### 1.2 解决方案结构

```
解决方案
├── Netor.Cortana.Plugin.Abstractions   ← 插件接口定义（netstandard2.0）
├── Netor.Cortana                       ← 宿主：加载插件、注入 AI 工具
├── Netor.Cortana.Entitys               ← 数据实体（不变）
└── KokoroAudition                      ← 音频模块（不变）
```

### 1.3 三种插件运行时模式

宿主同时支持三种插件类型，通过 `plugin.json` 中的 `runtime` 字段区分：

| 模式 | 标识 | 加载方式 | 适用语言 | 隔离级别 |
|------|------|---------|---------|---------|
| .NET 托管 | `dotnet` | `AssemblyLoadContext` | C#、F#、VB | 同进程（ALC 隔离） |
| 原生 DLL | `native` | `NativeLibrary.Load` | C# AOT、Rust、C++、Go | 同进程（函数级） |
| 子进程 | `process` | `Process` + stdin/stdout JSON-RPC | Python、Node.js、Java、任意 | 独立进程（最强） |

三种模式在宿主内部统一抽象为 `PluginInstance`，对 AI Agent 透明：

```
                    ┌─────────────────────────────────┐
                    │          AIAgentFactory          │
                    │   AIContextProviders = [...]     │
                    └──────────────┬──────────────────┘
                                   │
                    ┌──────────────┴──────────────────┐
                    │      PluginContextProvider       │
                    │  (适配 PluginInstance → AI 工具)  │
                    └──────────────┬──────────────────┘
                                   │
              ┌────────────────────┼────────────────────┐
              ▼                    ▼                     ▼
    ┌──────────────────┐ ┌─────────────────┐ ┌──────────────────┐
    │ DotNetPluginHost │ │ NativePluginHost│ │ ProcessPluginHost│
    │ (ALC 加载 IL DLL)│ │ (原生 DLL 导出) │ │ (子进程 JSON-RPC)│
    └──────────────────┘ └─────────────────┘ └──────────────────┘
```

### 1.4 插件目录结构

```
{UserDataDirectory}/
  plugins/
    weather-dotnet/              ← .NET 托管插件
    │   ├── plugin.json          { "runtime": "dotnet" }
    │   └── WeatherPlugin.dll
    │
    code-tools-native/           ← 原生 DLL 插件（C# AOT / Rust / C++）
    │   ├── plugin.json          { "runtime": "native" }
    │   └── code_tools.dll
    │
    translate-python/            ← 子进程插件
    │   ├── plugin.json          { "runtime": "process", "command": "python main.py" }
    │   └── main.py
```

---

## 二、接口设计

### 2.1 IPlugin 接口（Abstractions 项目，已实现）

```csharp
// Netor.Cortana.Plugin.Abstractions — netstandard2.0
public interface IPlugin
{
    string Id { get; }                        // 唯一标识（建议 reverse-domain）
    string Name { get; }                      // 显示名称
    Version Version { get; }                  // 语义版本
    string Description { get; }               // 描述
    string? Instructions { get; }             // AI 系统指令片段（可选）
    IReadOnlyList<string> Tags { get; }       // 分类标签（可选，用于按需激活）
    IReadOnlyList<AITool> Tools { get; }      // 工具列表
    Task InitializeAsync(IPluginContext context);  // 初始化
}
```

### 2.2 IPluginContext 接口（已实现）

宿主向插件暴露的有限上下文，避免插件直接访问宿主内部：

```csharp
public interface IPluginContext
{
    string DataDirectory { get; }             // 插件专属数据目录
    string WorkspaceDirectory { get; }        // 当前工作区目录
    ILoggerFactory LoggerFactory { get; }     // 日志工厂
    IHttpClientFactory HttpClientFactory { get; } // HTTP 客户端工厂
}
```

### 2.3 为什么 Abstractions 用 netstandard2.0？

- `netstandard2.0` 能被 net6.0、net8.0、net10.0、未来的 net11.0 **全部引用**
- 插件开发者不需要和宿主用同一个 TFM，**最大化兼容性**
- `Microsoft.Extensions.AI.Abstractions` 本身支持 netstandard2.0

### 2.4 原生/子进程插件的标准协议

对于非 .NET 托管插件（native 和 process），通过 `plugin.json` + 标准导出函数/JSON-RPC 定义等效的接口契约：

**原生 DLL 必须导出的 C 函数：**

| 导出函数 | 签名 | 用途 |
|---------|------|------|
| `cortana_plugin_get_info` | `() → char*` | 返回插件信息 JSON |
| `cortana_plugin_init` | `(char* configJson) → int` | 初始化，返回 0 成功 |
| `cortana_plugin_invoke` | `(char* toolName, char* argsJson) → char*` | 调用工具 |
| `cortana_plugin_free` | `(char* ptr) → void` | 释放插件分配的内存 |
| `cortana_plugin_destroy` | `() → void` | 销毁插件释放资源 |

**子进程 JSON-RPC 协议：**

```json
// 宿主 → 插件：获取信息
{"jsonrpc":"2.0","method":"getInfo","id":1}

// 宿主 → 插件：调用工具
{"jsonrpc":"2.0","method":"invoke","id":2,
 "params":{"tool":"get_weather","args":{"city":"北京"}}}

// 插件 → 宿主：返回结果
{"jsonrpc":"2.0","id":2,"result":{"content":"北京今天晴，25°C"}}
```

---

## 三、加载机制

### 3.1 .NET 托管模式（runtime: dotnet）

使用可卸载的 `AssemblyLoadContext` 实现隔离和热插拔：

```csharp
// 每个插件目录创建独立的 ALC
var alc = new PluginLoadContext(pluginDllPath, isCollectible: true);

// AssemblyDependencyResolver 优先从插件目录解析依赖
// 解析不到 → 回退到 Default ALC（共享 IPlugin 等接口类型）
```

**扫描策略 — 一个目录可包含多个插件类：**

```
code-tools/
└── CodeToolsPlugin.dll
    ├── class CSharpPlugin : IPlugin     → 12 个工具
    ├── class PythonPlugin : IPlugin     → 10 个工具
    └── class JavaPlugin : IPlugin       → 6 个工具

扫描结果：3 个插件实例，28 个工具，全部加载
```

遍历目录下所有 DLL → 扫描所有实现 `IPlugin` 的公共非抽象类 → 全部实例化并加入列表。

### 3.2 原生 DLL 模式（runtime: native）

使用 `NativeLibrary.Load` 加载原生 DLL 并通过函数指针调用导出函数：

```csharp
var handle = NativeLibrary.Load(pluginDllPath);
var getInfoPtr = NativeLibrary.GetExport(handle, "cortana_plugin_get_info");
var invokePtr  = NativeLibrary.GetExport(handle, "cortana_plugin_invoke");
// 通过 Marshal.GetDelegateForFunctionPointer 转为委托调用
```

工具列表从 `cortana_plugin_get_info` 返回的 JSON 解析获取。

**此模式天然兼容 .NET AOT 编译的 DLL**（见第六章详述）。

### 3.3 子进程模式（runtime: process）

启动插件可执行文件为子进程，通过 stdin/stdout 以 JSON-RPC 协议通信：

```csharp
var process = new Process {
    StartInfo = {
        FileName = command,
        RedirectStandardInput = true,
        RedirectStandardOutput = true
    }
};
// 发送 JSON 请求到 stdin，从 stdout 读取 JSON 响应
```

### 3.4 统一适配 — PluginContextProvider

无论哪种模式加载的插件，最终都包装为 `AIContextProvider` 注入 AI Agent：

```csharp
internal sealed class PluginContextProvider : AIContextProvider
{
    private readonly IPlugin _plugin;  // 或 NativePluginWrapper / ProcessPluginWrapper

    protected override ValueTask<AIContext> ProvideAIContextAsync(...)
    {
        return ValueTask.FromResult(new AIContext
        {
            Instructions = _plugin.Instructions,
            Tools = _plugin.Tools.ToList()
        });
    }
}
```

在 `AIAgentFactory.Build()` 中合并内置 Provider + 插件 Provider：

```csharp
var providers = new List<AIContextProvider> { /* 内置 7 个 */ };
foreach (var plugin in pluginLoader.GetActivePlugins())
    providers.Add(new PluginContextProvider(plugin));
```

---

## 四、热插拔生命周期

### 4.1 流程图

```
┌─────────────────────────────────────────────────┐
│               FileSystemWatcher                 │
│            监视 plugins/ 目录变化                 │
└────────────┬───────────────┬────────────────────┘
             │ 新增插件目录   │ 删除/更新插件目录
             ▼               ▼
     ┌───────────────┐  ┌────────────────────┐
     │ 创建加载上下文  │  │ 卸载旧插件          │
     │ 加载 DLL/进程  │  │ Dispose / Unload   │
     │ InitializeAsync│  │ (更新时) 重新加载    │
     └───────┬───────┘  └─────────┬──────────┘
             │                    │
             ▼                    ▼
     ┌─────────────────────────────────────┐
     │ 通过 EventHub 发布 OnPluginsChanged  │
     │ → AIAgentFactory 下次 Build 时       │
     │   自动获取最新的插件列表              │
     └─────────────────────────────────────┘
```

### 4.2 各模式的卸载方式

| 模式 | 卸载方式 | 说明 |
|------|---------|------|
| dotnet | `IPlugin.Dispose()` → `AssemblyLoadContext.Unload()` | GC 回收后程序集释放 |
| native | `cortana_plugin_destroy()` → `NativeLibrary.Free(handle)` | 立即释放 |
| process | 发送退出命令 → `Process.Kill()` | 进程终止 |

### 4.3 策略说明

- 插件变更**不中断正在进行的对话**，只影响下一次 `Build()` 调用
- 如需立即生效，可通过 EventHub 发布事件让 AiChatService 重建当前 Agent

---

## 五、兼容性分析（仅限 dotnet 模式）

### 5.1 第三方 DLL 版本冲突

| 情况 | 结果 |
|------|------|
| 宿主没有该库，插件带了 | 插件从自己目录加载 ✅ |
| 宿主有同名库，插件也带了 | 插件优先加载自己的版本（ALC 隔离）✅ |
| 插件没带该库，宿主有 | 回退到宿主版本 ⚠️ 版本差异大可能有问题 |

**规则**：插件接口方法签名只使用基元类型、Abstractions 中的类型、`Microsoft.Extensions.AI.Abstractions` 中的类型。**绝不在接口中暴露第三方库类型**。

### 5.2 系统 DLL 版本

`System.*`、`Microsoft.Extensions.*` 等运行时核心库**始终从 Default ALC 加载**，即使插件目录带了也会被忽略。✅ 完全安全。

### 5.3 C# 语言版本

C# 版本是编译时概念，编译后都是 IL 字节码。运行时不感知源码使用的 C# 版本。✅ 完全无影响。

### 5.4 .NET 目标框架版本（TFM）

| 插件 TFM | 宿主 TFM (net10.0) | 能否加载 |
|---|---|---|
| net6.0 | net10.0 | ✅ 可以（向下兼容） |
| net8.0 | net10.0 | ✅ 可以 |
| netstandard2.0 | net10.0 | ✅ 可以 |
| net10.0 | net10.0 | ✅ 可以 |
| net11.0+ | net10.0 | ❌ 可能失败（使用了宿主运行时没有的新 API） |
| net48 (.NET Framework) | net10.0 | ❌ 不兼容 |

### 5.5 发布方式兼容性

| 插件发布方式 | 包含 IL？ | dotnet 模式能加载？ | native 模式能加载？ |
|---|---|---|---|
| `dotnet build` | ✅ | ✅ | — |
| `dotnet publish` | ✅ | ✅ | — |
| `PublishReadyToRun=true` | ✅（保留 IL） | ✅ | — |
| `PublishTrimmed=true` | ✅（可能裁掉类型） | ⚠️ 需配置保留 | — |
| `PublishSingleFile=true` | ✅（打包进 exe） | ❌ 无法直接加载 | — |
| `PublishAot=true` | ❌ 纯原生码 | ❌ BadImageFormatException | ✅ 可以 |

---

## 六、AOT 插件支持方案

### 6.1 问题

Native AOT 编译产出的是原生机器码 DLL，不包含 IL 字节码，`AssemblyLoadContext` 无法加载。

### 6.2 解决方案：走 native 通道

.NET AOT 编译的 DLL 与 Rust/C++/Go 编译的原生 DLL **本质相同**。C# 通过 `[UnmanagedCallersOnly]` 特性可以导出 C 风格函数，被 `NativeLibrary.Load` 加载：

```csharp
// 插件开发者代码 — AOT 编译后导出 C 函数
using System.Runtime.InteropServices;

public static class PluginExports
{
    [UnmanagedCallersOnly(EntryPoint = "cortana_plugin_get_info")]
    public static IntPtr GetInfo()
    {
        var json = """{"id":"com.example.weather","name":"天气查询","tools":[...]}""";
        return Marshal.StringToCoTaskMemUTF8(json);
    }

    [UnmanagedCallersOnly(EntryPoint = "cortana_plugin_invoke")]
    public static IntPtr Invoke(IntPtr toolNamePtr, IntPtr argsJsonPtr)
    {
        var toolName = Marshal.PtrToStringUTF8(toolNamePtr)!;
        var argsJson = Marshal.PtrToStringUTF8(argsJsonPtr)!;
        var result = /* 执行逻辑 */;
        return Marshal.StringToCoTaskMemUTF8(result);
    }

    [UnmanagedCallersOnly(EntryPoint = "cortana_plugin_free")]
    public static void Free(IntPtr ptr) => Marshal.FreeCoTaskMem(ptr);

    [UnmanagedCallersOnly(EntryPoint = "cortana_plugin_destroy")]
    public static void Destroy() { /* 释放资源 */ }
}
```

插件 csproj 配置：

```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <OutputType>Library</OutputType>
</PropertyGroup>
```

### 6.3 宿主侧完全透明

对宿主来说，**无法区分** AOT C# DLL 和 Rust DLL，加载方式完全一样：

```csharp
var handle = NativeLibrary.Load(pluginDllPath);
var getInfoPtr = NativeLibrary.GetExport(handle, "cortana_plugin_get_info");
// 与加载 Rust/C++ DLL 完全相同
```

### 6.4 C# 插件开发者的两种选择

| 选择 | runtime 模式 | 发布方式 | 优势 |
|------|-------------|---------|------|
| 普通编译 | `dotnet` | `dotnet publish` | 开发简单，直接实现 IPlugin 接口 |
| AOT 编译 | `native` | `dotnet publish -p:PublishAot=true` | 极致性能、不暴露 IL/源码 |

---

## 七、安全策略

| 风险 | 缓解措施 |
|------|---------|
| 恶意插件执行危险操作 | `IPluginContext` 只暴露有限能力，不给 `IServiceProvider` |
| 插件崩溃拖垮宿主 | 工具执行包裹 try/catch，返回错误信息给 AI |
| 版本冲突 | dotnet 模式用独立 ALC 隔离；native/process 天然隔离 |
| 插件占用资源不释放 | `Dispose` / `destroy` 时强制清理；可加超时机制 |
| 加载到 AOT DLL 报错 | 捕获 `BadImageFormatException`，跳过并记录日志 |
| 原生 DLL 内存泄漏 | 要求插件导出 `cortana_plugin_free`，宿主每次调用后释放 |
| plugin.json 缺失或格式错误 | 校验失败则跳过该目录，记录警告日志 |

---

## 八、plugin.json 规范

### 8.1 通用字段

```json
{
  "id": "com.example.weather",
  "name": "天气查询",
  "version": "1.0.0",
  "description": "为 AI 添加天气查询能力",
  "runtime": "dotnet | native | process",
  "minHostVersion": "1.0.0",
  "abstractionsVersion": "1.0.0"
}
```

### 8.2 dotnet 模式扩展字段

```json
{
  "runtime": "dotnet",
  "assemblyName": "WeatherPlugin.dll",
  "targetFramework": "net10.0"
}
```

### 8.3 native 模式扩展字段

```json
{
  "runtime": "native",
  "libraryName": "weather_plugin.dll",
  "tools": [
    {
      "name": "get_weather",
      "description": "查询指定城市的当前天气",
      "parameters": [
        { "name": "city", "type": "string", "description": "城市名称", "required": true }
      ]
    }
  ]
}
```

> native 模式的 tools 字段为可选。如提供则宿主直接使用，否则从 `cortana_plugin_get_info` 动态获取。

### 8.4 process 模式扩展字段

```json
{
  "runtime": "process",
  "command": "python main.py",
  "tools": [
    {
      "name": "translate_text",
      "description": "翻译文本",
      "parameters": [
        { "name": "text", "type": "string", "required": true },
        { "name": "targetLang", "type": "string", "required": true }
      ]
    }
  ]
}
```

### 8.5 宿主加载前校验规则

1. `plugin.json` 必须存在且格式正确，否则跳过
2. `runtime` 必须是 `dotnet`、`native`、`process` 之一
3. dotnet 模式：`targetFramework` 不高于当前运行时版本
4. `abstractionsVersion` 与宿主兼容
5. `minHostVersion` ≤ 当前宿主版本

---

## 九、与现有系统的关系

### 9.1 与 AgentSkillsProvider 的关系

| | AgentSkillsProvider | 插件系统 |
|---|---|---|
| 形态 | JSON/YAML 技能文件 | 编译后的 DLL 或独立进程 |
| 能力 | Prompt 模板、固定参数 | 任意代码、网络调用、本地 API |
| 热更新 | 替换文件即可 | 替换目录 + 重载 |
| 适合场景 | 简单的 prompt 工具 | 复杂逻辑、需要外部 SDK |

两者共存，互不冲突。简单的用技能文件，复杂的用插件。

### 9.2 与现有 AIContextProvider 的关系

现有 7 个内置 Provider（AgentSkillsProvider、FileMemoryProvider、PowerShellProvider 等）保持不变。插件通过 `PluginContextProvider` 桥接后，与内置 Provider **平级并列**注入到 `AIAgentFactory.Build()` 的 `AIContextProviders` 列表中。

### 9.3 单文件发布兼容性

| 项目 | 发布方式 | 说明 |
|------|---------|------|
| Netor.Cortana | 单文件 EXE（不变） | 插件系统代码随宿主打包 |
| Abstractions | 随宿主一起打包 | 作为项目引用 |
| 插件 DLL | **不打包进单文件** | 独立存在于 plugins/ 目录 |

### 9.4 工具数量膨胀的应对

当插件带来的工具总数过多时（>60 个），每次对话发送全部工具描述会增大 token 消耗，降低 AI 选择准确率。

应对方案：通过 `IPlugin.Tags` 属性实现按需激活。宿主可根据对话上下文只激活相关分类的插件。**第一版可不实现，全部加载。**

---

## 十、实施路线图

### 第一阶段：核心框架

1. ~~创建 `Netor.Cortana.Plugin.Abstractions` 项目（已完成）~~
2. 在宿主中实现 `PluginLoader`（ALC 加载 + FileSystemWatcher）
3. 实现 `PluginContextProvider` 桥接
4. 修改 `AIAgentFactory.Build()` 合并内置 Provider + 插件 Provider
5. 在 `Program.cs` 注册 `PluginLoader`
6. 定义 `plugin.json` 标准格式（预留 runtime 字段）
7. 编写一个示例 .NET 插件验证链路

### 第二阶段：子进程插件

8. 实现 `ProcessPluginHost`（子进程 + JSON-RPC 通信）
9. 编写 Python / Node.js 示例插件

### 第三阶段：原生插件

10. 实现 `NativePluginHost`（NativeLibrary + 函数指针）
11. 编写 C# AOT / Rust 示例插件

### 第四阶段：增强功能（可选）

12. 插件管理 UI（设置窗口中展示已加载插件列表）
13. 按标签智能激活工具
14. 插件市场 / 在线安装
