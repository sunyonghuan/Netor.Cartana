# Netor.Cortana 模块化重构方案

> 历史说明：本文档面向旧 Netor.Cortana UI 主体的模块化拆分设计，不等价于当前 AvaloniaUI 主项目的最终实现。使用时请结合根目录 README 和 Docs 最新文档判断哪些结论仍然有效。

## 一、现状全景

### 1.1 当前项目结构（Netor.Cortana 主项目：8917 行 C#）

| 职责域 | 文件 | 行数 | 核心依赖 |
|--------|------|------|---------|
| **AI 推理** | `AiChatService.cs` | 293 | WebSocketServer、AIAgentFactory、CortanaDbContext |
| | `AIAgentFactory.cs` | ~150 | 所有 Provider、PluginLoader、Desktop Provider |
| | `AiModelFetcherService.cs` | ~100 | HttpClient、AiModelService |
| | `VoiceChatService.cs` | 339 | AIAgentFactory、TextToSpeechService、WebSocketServer |
| **AI Provider** | `ChatHistoryDataProvider.cs` | 236 | CortanaDbContext、SystemSettingsService |
| | `FileMemoryProvider.cs` | ~80 | App.WorkspaceDirectory |
| | `CortanaProvider.cs` | 374 | **App.MainWindow**、ProviderService、ModelService、AgentService |
| **语音** | `WakeWordService.cs` | ~150 | SherpaOnnx、NAudio、App.UserDataDirectory |
| | `SpeechRecognitionService.cs` | ~200 | SherpaOnnx、NAudio、App.UserDataDirectory |
| | `TextToSpeechService.cs` | 487 | SherpaOnnx、NAudio、App.UserDataDirectory |
| **网络** | `WebSocketServer.cs` | 347 | HttpListener |
| **事件** | `Events.cs` | ~90 | Netor.EventHub |
| **桌面控制** | PowerShell 全套（4文件） | ~530 | — |
| | WindowManagement 全套（3文件） | ~540 | — |
| | FileBrowser 全套（3文件） | ~600 | — |
| | ApplicationLauncher 全套（3文件） | ~290 | — |
| **UI 窗口** | MainWindow + BridgeHostObject | ~420 | WinFormedge、AiChatService |
| | FloatWindow + BridgeHostObject | ~100 | WinFormedge |
| | SettingsWindow + BridgeHostObject | ~450 | 全部 Service |
| | WakeWordBubbleWindow | 235 | WinFormedge |
| **插件** | PluginLoader + 子目录 | ~650 | IPlugin、MCP |
| **启动/配置** | App.cs、App.Startup.cs、Program.cs | ~480 | 全局编排 |

### 1.2 关键耦合点（19 个文件引用 `App.xxx`）

| 静态依赖 | 引用文件数 | 问题 |
|---------|-----------|------|
| `App.Services`（ServiceLocator） | **10** | 最严重：AI/Voice/Plugin 层直接 resolve DI，无法独立编译 |
| `App.UserDataDirectory` | 5 | 语音3个 + FileMemory + PluginLoader |
| `App.WorkspaceDirectory` | 4 | FileMemory、PluginLoader、AIAgentFactory |
| `App.MainWindow` | 2 | CortanaProvider.Show/Hide、FloatWindow.ShowMainWindow |
| `App.Map()`（URI映射） | 4 | 4个窗口构造函数 |
| `App.CancellationTokenSource` | 2 | AiChatService、App.cs |
| `Events.OnXxx`（事件ID） | **9** | 跨语音/AI/UI/插件全域引用 |

---

## 二、重构目标

1. **UI 壳只管 UI**：窗口创建、BridgeHostObject、前端资源。不直接引用 AI/语音/桌面控制的具体实现
2. **各业务层通过 EventHub 事件通信**：UI 层发布用户操作事件，业务层订阅处理后发布结果事件，UI 层订阅刷新界面
3. **消除 `App.Services` ServiceLocator 模式**：全部改为构造函数 DI 注入
4. **Events 放到 Entitys 层**：事件 ID 和参数类型是纯数据结构，供所有层共用

---

## 三、目标项目结构

```
解决方案 Netor.Cortana.sln
│
├── Src/
│   ├── Netor.Cortana.Entitys/          ← 已有：实体 + DbContext + Service + Events + IAppPaths
│   ├── Netor.Cortana.AI/               ← 新建：AI 推理 + Agent 构建
│   ├── Netor.Cortana.Voice/            ← 新建：KWS + STT + TTS
│   ├── Netor.Cortana.Plugin/           ← 新建：插件加载框架（扫描/加载/卸载/热插拔/MCP）
│   ├── Netor.Cortana.Networks/         ← 新建：网络通信（WebSocket + 未来网络中转站）
│   └── Netor.Cortana/                  ← 瘦身：UI 壳 + DI 编排 + CortanaProvider（自我操作工具）
│
├── Src/Plugins/                        ← 已有插件基础设施，不动
│   ├── Netor.Cortana.Plugin.Abstractions/
│   ├── Netor.Cortana.NativeHost/
│   ├── Netor.Cortana.Plugin.Native/
│   └── Netor.Cortana.Plugin.Native.Generator/
│
├── Samples/                            ← 不动
│
└── 未来：独立 AOT 原生插件（基于现有插件框架）
    ├── Cortana.Plugin.PowerShell/      ← 从内置功能拆出为 AOT 插件
    ├── Cortana.Plugin.WindowManager/   ← 同上
    ├── Cortana.Plugin.FileBrowser/     ← 同上
    └── Cortana.Plugin.AppLauncher/     ← 同上
```

### 3.1 项目依赖图

```
Netor.Cortana（UI壳 / WinExe / net10.0-windows）
    ├─→ Netor.Cortana.AI           ← 仅 DI 注册（.AddCortanaAI()）
    ├─→ Netor.Cortana.Voice        ← 仅 DI 注册（.AddCortanaVoice()）
    ├─→ Netor.Cortana.Plugin       ← 仅 DI 注册（.AddCortanaPlugin()）
    ├─→ Netor.Cortana.Networks     ← 仅 DI 注册（.AddCortanaNetworks()）
    └─→ Netor.Cortana.Entitys      ← 事件定义、接口契约

Netor.Cortana.AI（AI推理 + Agent构建）
    ├─→ Netor.Cortana.Entitys      ← 事件发布/订阅
    └─→ Netor.Cortana.Plugin       ← AIAgentFactory 获取插件 Provider

Netor.Cortana.Voice（语音引擎）
    └─→ Netor.Cortana.Entitys      ← 事件发布/订阅

Netor.Cortana.Plugin（插件加载框架）
    ├─→ Netor.Cortana.Entitys
    └─→ Plugin.Abstractions

Netor.Cortana.Networks（网络通信 + 事件中转）
    └─→ Netor.Cortana.Entitys      ← 事件订阅 → WebSocket 推送
```

### 3.2 核心通信模式

**所有模块间通过 EventHub 事件通信，零直接引用**（AI ↔ Voice ↔ Networks ↔ UI 之间）。

UI 壳在 `Program.cs` 中通过各模块的 DI 扩展方法一行注册：

```csharp
services
    .AddCortanaVoice()
    .AddCortanaAI()
    .AddCortanaPlugin()
    .AddCortanaNetworks();
```

### 3.3 CortanaProvider（自我操作工具）归属

`CortanaProvider` 提供"打开窗口、关闭窗口、移动窗口"等与 UI 壳深度绑定的 AI 工具。
**保留在 UI 壳**，通过 `AddCortanaPlugin()` 注入时以 `IEnumerable<AIContextProvider>` 收集所有 DI 注册的 Provider（包括 UI 壳追加的）。

### 3.4 桌面功能插件化路线

PowerShell、窗口管理、文件浏览、应用启动等内置功能，**后续**将从内置代码拆出为独立 AOT 原生插件。
基于现有 NativeHost 插件框架，追求极致效率和性能。本次重构先迁移到 Plugin 项目内保留，待插件框架验证后再 AOT 化。

---

## 四、Events 迁移到 Entitys 层

### 4.1 为什么放 Entitys

`Events.cs` 里全是纯数据结构：事件 ID（字符串标识）和事件参数（record）。
它们没有任何业务逻辑，是 AI 层、Voice 层、UI 层之间的**通信契约**。
放在 Entitys 层可以被所有项目引用而不引入循环依赖。

### 4.2 Entitys 需要新增 NuGet 依赖

```xml
<PackageReference Include="Netor.EventHub" Version="1.2.5" />
```

---

## 五、消除 `App.xxx` 静态依赖

### 5.1 新增 `IAppPaths` 接口（放 Entitys 层）

```csharp
/// <summary>
/// 应用程序路径契约，替代 App.xxx 静态属性。
/// </summary>
public interface IAppPaths
{
    /// <summary>工作区目录（用户可配置）。</summary>
    string WorkspaceDirectory { get; }

    /// <summary>用户数据目录（exe 所在目录）。</summary>
    string UserDataDirectory { get; }

    /// <summary>插件目录。</summary>
    string PluginDirectory { get; }
}
```

UI 壳 `App` 类实现 `IAppPaths`，在 DI 中注册 `services.AddSingleton<IAppPaths>(App实例)`。
各层通过构造函数注入 `IAppPaths` 替代 `App.UserDataDirectory` 等静态调用。

### 5.2 消除 `App.Services`（ServiceLocator）

| 文件 | 当前用法 | 改造方式 |
|------|---------|---------|
| `AIAgentFactory.cs` | `App.Services.GetRequiredService<>()` 6处 | 构造函数注入所有 Provider |
| `AiChatService.cs` | `App.Services.GetRequiredService<>()` | 构造函数注入 |
| `CortanaProvider.cs` | `App.Services.GetRequiredService<>()` 4处 | 构造函数注入 |
| `VoiceChatService.cs` | `App.Services.GetRequiredService<>()` 3处 | 构造函数注入 |
| `ChatHistoryDataProvider.cs` | 1处残余 | 去除 |
| `MainWindow.BridgeHostObject.cs` | 多处 | 保留（COM 对象特殊性，见下方说明） |
| `SettingsWindow.BridgeHostObject.cs` | 多处 | 保留（同上） |
| `PluginContext.cs` | `App.Services` | 构造函数传入 |
| `WakeWordBubbleWindow.cs` | `App.Services` | 构造函数注入 |

> **BridgeHostObject 例外**：COM `[ComVisible]` 对象由 WebView2 实例化，无法走 DI。
> 保留 `App.Services` 但仅限于 UI 壳内的 Bridge 类，改为构造时一次性获取所需服务引用。

### 5.3 `CortanaProvider` 保留在 UI 壳

`CortanaProvider` 调用了 `App.MainWindow.Show()`、`FloatWindow` 等 UI 操作。
它是 AI 的工具提供者，与 UI 窗口深度绑定。

**决策**：`CortanaProvider` **保留在 UI 壳**（详见第三章 3.3 节）。
因为它本质上是"Cortana 操作自身"的工具，与 UI 窗口不可分离。

仍需 `IWindowController` 接口（放 Entitys 层），用于其他模块查询窗口状态：

```csharp
public interface IWindowController
{
    void ShowMainWindow();
    void HideMainWindow();
    void ShowSettingsWindow();
    void ShowFloatWindow();
    void MoveFloatWindow(int x, int y);
    bool IsMainWindowVisible();
}
```

UI 壳实现此接口，通过 DI 注册。
`VoiceChatService` 中 `Application.OpenForms[0].Visible` 改用 `IWindowController.IsMainWindowVisible()`。

---

## 六、事件驱动数据流设计

### 6.1 AI → Voice 事件驱动流水线

**现状**：`VoiceChatService` 直接调用 `ttsService.EnqueueTextAsync(sentence)` 和 `ttsService.FinishAndWaitAsync()`，
AI 层与 Voice 层形成方法级耦合。

**目标**：AI 层通过事件发布断句文本，Voice 层通过事件订阅消费，两者完全解耦。

#### 事件流时序图

```
AI 推理层（VoiceChatService）              Voice 层（TextToSpeechService）               UI 层
    │                                          │                                        │
    │── 逐 token 收集，遇句末标点 ──→           │                                        │
    │   publish(OnTtsEnqueue, "你好世界。")      │                                        │
    │──────────────────────────────────────────→│                                        │
    │                                          │── 合成队列消费，合成语音 ──→              │
    │                                          │── 播放队列消费，开始播放 ──→              │
    │                                          │   publish(OnTtsSubtitle, "你好世界。")   │
    │                                          │─────────────────────────────────────────→│
    │                                          │                                 字幕显示该句│
    │── 继续收集 token ──→                      │                                        │
    │   publish(OnTtsEnqueue, "今天天气不错。")   │                                        │
    │──────────────────────────────────────────→│                                        │
    │                                          │── 合成 + 播放 ──→                       │
    │                                          │   publish(OnTtsSubtitle, "今天天气不错。")│
    │                                          │─────────────────────────────────────────→│
    │                                          │                                 字幕更新该句│
    │── AI 推理结束 ──→                         │                                        │
    │   publish(OnTtsFinish)                   │                                        │
    │──────────────────────────────────────────→│                                        │
    │                                          │── 合成队列：收到结束信号 ──→              │
    │                                          │   不再等待新文本，消费完剩余 ──→          │
    │                                          │── 播放队列：全部播放完毕 ──→              │
    │                                          │   publish(OnTtsCompleted)               │
    │                                          │─────────────────────────────────────────→│
    │                                          │                                 对话结束  │
```

#### 新增事件定义

```csharp
// ──────── AI → Voice 事件驱动 ────────

/// <summary>AI 断句完成，请求 TTS 合成并播放该句文本。</summary>
internal static TtsEnqueueEvent OnTtsEnqueue = new("voice.tts.enqueue");

/// <summary>AI 推理完成，没有后续文本了。Voice 合成完剩余队列即可。</summary>
internal static VoiceSignalEvent OnTtsFinish = new("voice.tts.finish");

// 事件类型
public record TtsEnqueueEvent(string Eventid) : EventID<TtsEnqueueArgs>(Eventid);
public record TtsEnqueueArgs(string Sentence) : EventArgs;
```

#### 改造前后对比

| 维度 | 改造前（直接调用） | 改造后（事件驱动） |
|------|------------------|------------------|
| 耦合 | `VoiceChatService` 注入 `TextToSpeechService` | 零直接引用，仅共享事件定义 |
| 断句 → 合成 | `await ttsService.EnqueueTextAsync(sentence)` | `publisher.Publish(OnTtsEnqueue, new(sentence))` |
| 推理结束 | `await ttsService.FinishAndWaitAsync()` | `publisher.Publish(OnTtsFinish, ...)` |
| 合成入口 | `EnqueueTextAsync` 由外部直接调用 | `TextToSpeechService` 订阅 `OnTtsEnqueue`，自行写入 Channel |
| 结束信号 | `FinishAndWaitAsync` 关闭 Channel Writer | 订阅 `OnTtsFinish`，关闭 Channel Writer |
| 字幕 | `OnTtsSubtitle` 事件（已有） | 不变，播放每句时发布 |
| 全部完成 | `OnChatCompleted` | 播放队列清空后发布 `OnTtsCompleted` |

### 6.2 字幕显示机制

**关键设计**：字幕不是逐字显示，而是**逐句显示**。

原理：`TextToSpeechService` 的播放循环每播放一句，就发布 `OnTtsSubtitle` 事件（携带当前播放的句子文本）。
UI 层（WakeWordBubbleWindow 或 Networks → WebSocket → 前端）订阅该事件，更新字幕为当前正在播放的那一句。

```
播放线程：
  foreach (audio, text) in audioChannel:
      播放 audio
      publish(OnTtsSubtitle, text)   ← 当前句子的字幕
  publish(OnTtsCompleted)            ← 全部播放完毕
```

这意味着字幕切换的频率等于 TTS 句子的播放频率，用户看到的是一句一句跳转，
与语音播放完全同步——听到哪句就显示哪句。

### 6.3 Networks 层的事件中转

`WebSocketServer`（Networks 层）不再由 AI 层直接调用 `BroadcastAsync`。
改为订阅事件，将事件内容通过 WebSocket 推送到前端：

| 订阅事件 | WebSocket 推送消息类型 | 用途 |
|---------|---------------------|------|
| `OnSttPartial` | `stt_partial` | 语音识别中间结果 |
| `OnSttFinal` | `stt_final` | 语音识别最终结果 |
| `OnTtsSubtitle` | `tts_subtitle` | TTS 字幕（当前播放的句子） |
| `OnTtsCompleted` | `tts_completed` | TTS 全部完成 |
| `OnChatCompleted` | `chat_completed` | 对话轮次结束 |

前端 HTML 页面直接连接 WebSocket，不再通过 BridgeHostObject 轮询。

---

## 七、各新项目迁入的具体文件

### 7.1 Netor.Cortana.Voice（新建类库 / net10.0-windows）

**迁入文件：**

| 原路径（相对 Netor.Cortana 项目） | 新位置 |
|----------------------------------|--------|
| `Services/WakeWordService.cs` | `Netor.Cortana.Voice/WakeWordService.cs` |
| `Services/SpeechRecognitionService.cs` | `Netor.Cortana.Voice/SpeechRecognitionService.cs` |
| `Services/TextToSpeechService.cs` | `Netor.Cortana.Voice/TextToSpeechService.cs` |

**NuGet 包迁移**：`org.k2fsa.sherpa.onnx`、`NAudio.WinMM`

**改动点**：
- `App.UserDataDirectory` → 注入 `IAppPaths`
- 命名空间 `Netor.Cortana.Services` → `Netor.Cortana.Voice`
- Events 引用改为 `using Netor.Cortana.Entitys`（Events 已迁移到 Entitys）
- 访问修饰符 `internal` → `public`（跨程序集可见性）
- **`TextToSpeechService` 新增事件订阅**：
  - 订阅 `OnTtsEnqueue` → 写入文本 Channel（替代原来的外部 `EnqueueTextAsync` 调用）
  - 订阅 `OnTtsFinish` → 关闭文本 Channel Writer（替代原来的外部 `FinishAndWaitAsync` 调用）
  - `EnqueueTextAsync` / `FinishAndWaitAsync` 可保留为内部方法供事件处理器调用

### 7.2 Netor.Cortana.AI（新建类库 / net10.0-windows）

**迁入文件：**

| 原路径 | 新位置 |
|--------|--------|
| `Services/AiChatService.cs` | `Netor.Cortana.AI/AiChatService.cs` |
| `Services/AIAgentFactory.cs` | `Netor.Cortana.AI/AIAgentFactory.cs` |
| `Services/AiModelFetcherService.cs` | `Netor.Cortana.AI/AiModelFetcherService.cs` |
| `Services/VoiceChatService.cs` | `Netor.Cortana.AI/VoiceChatService.cs` |
| `Providers/ChatHistoryDataProvider.cs` | `Netor.Cortana.AI/Providers/ChatHistoryDataProvider.cs` |
| `Providers/FileMemoryProvider.cs` | `Netor.Cortana.AI/Providers/FileMemoryProvider.cs` |
| `Services/AppSettings.cs` | `Netor.Cortana.AI/AppSettings.cs` |

> **注意**：`CortanaProvider.cs` **不迁入 AI 层**，保留在 UI 壳（详见 3.3 节）。

**NuGet 包迁移**：
- `Microsoft.Agents.AI` / `Microsoft.Agents.AI.OpenAI` / `Microsoft.Agents.AI.Workflows`
- `Microsoft.Extensions.AI`
- `OllamaSharp`

**重要改动点：**

| 改动 | 文件 | 说明 |
|------|------|------|
| `App.Services` → DI 注入 | `AIAgentFactory.cs` | 构造函数注入所有 Provider 实例 |
| `App.Services` → DI 注入 | `AiChatService.cs`、`VoiceChatService.cs` | 注入具体 Service |
| `App.WorkspaceDirectory` → `IAppPaths` | `AIAgentFactory.cs`、`FileMemoryProvider.cs` | 路径访问 |
| `App.CancellationTokenSource` → 注入 | `AiChatService.cs` | 改为构造函数传入或事件驱动 |
| `Application.OpenForms[0].Visible` | `VoiceChatService.cs` | 改用 `IWindowController.IsMainWindowVisible()` |
| **去掉 TTS 直接调用** | `VoiceChatService.cs` | 见下方详细说明 |
| **去掉 WebSocket 直接调用** | `VoiceChatService.cs` | 改为发布事件，由 Networks 层订阅推送 |

**VoiceChatService 核心改造**：

```csharp
// 改造前：直接调用 TTS
await ttsService.EnqueueTextAsync(sentence, cancellationToken);
await ttsService.FinishAndWaitAsync(cancellationToken);

// 改造后：发布事件
publisher.Publish(Events.OnTtsEnqueue, new TtsEnqueueArgs(sentence));
// ... AI 推理全部结束后 ...
publisher.Publish(Events.OnTtsFinish, new VoiceSignalArgs());
```

改造后 `VoiceChatService` 构造函数不再注入 `TextToSpeechService` 和 `WebSocketServer`，
仅依赖 `IPublisher`、`ISubscriber`、`AIAgentFactory`、`CortanaDbContext`、`IWindowController`。
AI 层与 Voice 层 / Networks 层实现零引用。

### 7.3 Netor.Cortana.Plugin（新建类库 / net10.0-windows）

**迁入文件：**

| 原路径 | 新位置 |
|--------|--------|
| `Plugins/PluginLoader.cs` | `Netor.Cortana.Plugin/PluginLoader.cs` |
| `Plugins/PluginContext.cs` | `Netor.Cortana.Plugin/PluginContext.cs` |
| `Plugins/McpClientManager.cs`（如有） | `Netor.Cortana.Plugin/McpClientManager.cs` |
| 桌面控制全套（暂保留，待后续 AOT 插件化） | `Netor.Cortana.Plugin/BuiltIn/...` |

**桌面控制文件暂迁入 Plugin/BuiltIn/ 目录：**

| 原路径 | 新位置 |
|--------|--------|
| `Providers/PowerShell/*` | `Plugin/BuiltIn/PowerShell/*` |
| `Providers/DesktopControl/WindowManagement/*` | `Plugin/BuiltIn/WindowManagement/*` |
| `Providers/DesktopControl/FileBrowser/*` | `Plugin/BuiltIn/FileBrowser/*` |
| `Providers/DesktopControl/ApplicationLauncher/*` | `Plugin/BuiltIn/ApplicationLauncher/*` |

> 桌面控制功能作为内置 Provider 暂时随 Plugin 项目编译。
> 后续独立为 AOT 原生插件后，从 `BuiltIn/` 目录移除。

**NuGet 包迁移**：`ModelContextProtocol`（MCP 客户端）

**改动点**：
- `App.Services` → 构造函数传入
- `App.WorkspaceDirectory` → `IAppPaths`
- 命名空间统一为 `Netor.Cortana.Plugin`

**DI 扩展方法**：

```csharp
public static class PluginServiceExtensions
{
    public static IServiceCollection AddCortanaPlugin(this IServiceCollection services)
    {
        services.AddSingleton<PluginLoader>();
        // 注册所有内置 Provider
        services.AddSingleton<AIContextProvider, PowerShellProvider>();
        services.AddSingleton<AIContextProvider, WindowManagerProvider>();
        services.AddSingleton<AIContextProvider, FileBrowserProvider>();
        services.AddSingleton<AIContextProvider, ApplicationLauncherProvider>();
        return services;
    }
}
```

### 7.4 Netor.Cortana.Networks（新建类库 / net10.0-windows）

**迁入文件：**

| 原路径 | 新位置 |
|--------|--------|
| `Services/WebSocketServer.cs` | `Netor.Cortana.Networks/WebSocketServer.cs` |

**NuGet 包**：无额外包（HttpListener 为 .NET 内置）。

**改动点**：
- 不再由 AI 层直接调用 `BroadcastAsync`
- 改为**事件订阅模式**：启动时订阅所有需要推送到前端的事件
- 收到事件后通过 WebSocket 广播到所有已连接的前端客户端

**事件订阅注册**：

```csharp
public sealed class WebSocketEventRelay(
    WebSocketServer server,
    ISubscriber subscriber)
{
    public void Start()
    {
        subscriber.Subscribe<VoiceTextArgs>(Events.OnSttPartial, async (args, _) =>
        {
            await server.BroadcastAsync("stt_partial", args.Text);
            return false;
        });

        subscriber.Subscribe<VoiceTextArgs>(Events.OnTtsSubtitle, async (args, _) =>
        {
            await server.BroadcastAsync("tts_subtitle", args.Text);
            return false;
        });

        subscriber.Subscribe<VoiceSignalArgs>(Events.OnTtsCompleted, async (_, _) =>
        {
            await server.BroadcastAsync("tts_completed", "");
            return false;
        });

        // ... 其他事件订阅
    }
}
```

**DI 扩展方法**：

```csharp
public static class NetworkServiceExtensions
{
    public static IServiceCollection AddCortanaNetworks(this IServiceCollection services)
    {
        services.AddSingleton<WebSocketServer>();
        services.AddSingleton<WebSocketEventRelay>();
        return services;
    }
}
```

### 7.5 Netor.Cortana.Entitys（已有项目，扩展）

| 新增内容 | 说明 |
|---------|------|
| `Events.cs` | 从主项目迁入，事件 ID + 参数类型（含新增的 `TtsEnqueueEvent`/`TtsEnqueueArgs`） |
| `IAppPaths.cs` | 新建接口，应用路径契约 |
| `IWindowController.cs` | 新建接口，窗口控制契约 |

**NuGet 新增**：`Netor.EventHub`

### 7.6 Netor.Cortana（UI壳，瘦身后保留）

| 保留内容 | 说明 |
|---------|------|
| `Pages/` 全部窗口和 BridgeHostObject | UI 核心 |
| `Providers/CortanaProvider.cs` | AI 自我操作工具（保留在 UI 壳，详见 3.3 节） |
| `App.cs` / `App.Startup.cs` | 应用生命周期 + `IAppPaths`/`IWindowController` 实现 |
| `Program.cs` | DI 注册入口（调用各模块的 `AddCortanaXxx()` 扩展方法） |
| `Extensions/` | 工具方法 |
| `globalusing.cs` | 全局引用 |
| `wwwroot/` | 前端资源 |

**移除的文件（已迁到对应项目）**：
- `Services/WakeWordService.cs` → Voice
- `Services/SpeechRecognitionService.cs` → Voice
- `Services/TextToSpeechService.cs` → Voice
- `Services/AiChatService.cs` → AI
- `Services/AIAgentFactory.cs` → AI
- `Services/AiModelFetcherService.cs` → AI
- `Services/VoiceChatService.cs` → AI
- `Services/WebSocketServer.cs` → Networks
- `Plugins/PluginLoader.cs` → Plugin
- `Providers/PowerShell/*` → Plugin/BuiltIn
- `Providers/DesktopControl/*` → Plugin/BuiltIn

**移除的 NuGet 包（已迁到对应项目）**：
- `org.k2fsa.sherpa.onnx` → Voice 项目
- `NAudio.WinMM` → Voice 项目
- `OllamaSharp` → AI 项目
- `Microsoft.Agents.AI.*` → AI 项目
- `ModelContextProtocol` → Plugin 项目

---

## 八、风险评估

| 风险 | 等级 | 影响 | 缓解 |
|------|------|------|------|
| `App.Services` 消除不彻底 | 🔴 高 | 10个文件19个调用点，遗漏会导致运行时null异常 | 逐文件 grep 验证，编译器会报错 |
| 事件驱动丢消息 | 🔴 高 | TtsEnqueue 事件发出但 Voice 未订阅，导致 AI 说了但没声音 | 启动顺序保证：Voice DI 先注册先初始化，订阅先于发布 |
| 事件驱动增加调试难度 | 🟡 中 | 事件流无法像方法调用那样单步跟踪 | 关键事件添加 structured logging，便于追踪流转 |
| 命名空间大面积变更 | 🟡 中 | 迁移后 using 全部要改 | 在 globalusing.cs 统一配置新命名空间 |
| `AIAgentFactory.Build()` 依赖图过深 | 🟡 中 | 注入 7+ 个 Provider，构造参数过长 | 使用 `IEnumerable<AIContextProvider>` 批量注入 |
| TTS 流水线时序 | 🟡 中 | 事件异步投递可能导致合成队列排序错乱 | EventHub 保证同一事件 ID 的顺序投递；AI 逐句发布保证顺序 |
| 插件系统归属 | 🟢 低 | PluginLoader + 内置 Provider 混合存放 | BuiltIn/ 目录明确标识，后续 AOT 化后移除 |
| SherpaOnnx 模型路径 | 🟢 低 | 模型文件在 csproj 中配置 Content 复制 | 模型 Content 项保留在 UI 壳 csproj，Voice 层只读取路径 |
| WinFormedge COM 桥接 | 🟢 低 | BridgeHostObject 保留在 UI 壳，不受影响 | 无需处理 |
| `internal` → `public` 可见性 | 🟢 低 | 跨程序集需要 public，扩大了 API 暴露面 | 用 `[EditorBrowsable(Never)]` 控制 |

---

## 九、实施阶段与工作量

| 阶段 | 内容 | 交互轮数 | 预估时间 |
|------|------|---------|---------|
| **A** | 基础设施：创建4个新项目骨架（AI/Voice/Plugin/Networks），配置引用和TFM，验证空编译 | 3-4 | 15-20分钟 |
| **B** | Entitys 扩展：迁入 Events.cs（含新增 TtsEnqueue/TtsFinish 事件）、新增 IAppPaths + IWindowController | 2-3 | 10-15分钟 |
| **C** | Voice 层迁移（3个文件 + IAppPaths 注入 + TTS 事件订阅改造） | 4-5 | 20-25分钟 |
| **D** | Plugin 层迁移（PluginLoader + 内置桌面 Provider 14个文件） | 4-5 | 20-25分钟 |
| **E** | Networks 层迁移（WebSocketServer + 事件中转 WebSocketEventRelay） | 2-3 | 10-15分钟 |
| **F** | AI 层迁移（7个文件，最复杂：VoiceChatService 事件化 + 消除 App.Services） | 6-8 | 35-45分钟 |
| **G** | UI壳瘦身：清理已迁文件、Program.cs 改用 AddCortanaXxx()、App.cs 实现接口、CortanaProvider DI 注册 | 3-4 | 15-20分钟 |
| **H** | 全量编译 + 修复遗留问题 + 事件流集成验证 | 3-4 | 15-20分钟 |
| **合计** | | **27-36** | **140-185分钟** |

**实施顺序的依据**：

1. **先 Entitys**（B）：所有项目的共同基础——事件定义、接口契约
2. **再 Voice**（C）：最独立，仅依赖 Entitys，改造后通过事件订阅接入
3. **然后 Plugin**（D）：桌面功能文件最多但改动最小（几乎无 `App.xxx`）
4. **再 Networks**（E）：简单，一个文件 + 一个事件中转类
5. **后 AI**（F）：依赖 Plugin（Provider 注入），且 VoiceChatService 事件化改造最复杂
6. **最后 UI壳**（G）：删除已迁文件，配置 DI，验证编译

---

## 十、关键设计决策总结

| 决策 | 理由 |
|------|------|
| **同进程类库拆分，不搞多进程** | LiteDB 单进程限制、VoiceChatService 流水线不可打断、桌面应用无需微服务 |
| **Events 放 Entitys** | 纯数据结构，被所有层引用，放最底层避免循环依赖 |
| **AI → Voice 完全事件驱动** | VoiceChatService 发布 TtsEnqueueEvent/TtsFinishEvent，TTS 订阅消费。零直接方法调用，AI 层不引用 Voice 层 |
| **字幕逐句显示，非逐字** | TTS 播放线程每播放一句发布 OnTtsSubtitle，UI 同步显示当前句。用户听到哪句看到哪句 |
| **Networks 事件订阅模式** | WebSocketServer 不被 AI 直接调用，改为订阅事件后自主推送。前端直连 WebSocket |
| **CortanaProvider 保留在 UI 壳** | 深度绑定窗口操作（Show/Hide/Move），本质是"Cortana 操作自身"，不适合放 AI 层 |
| **桌面功能暂入 Plugin/BuiltIn/** | 本次先随 Plugin 编译，后续基于 NativeHost 框架拆为独立 AOT 原生插件 |
| **各模块 DI 扩展方法** | `AddCortanaVoice()`/`AddCortanaAI()`/`AddCortanaPlugin()`/`AddCortanaNetworks()`，UI 壳一行注册 |
| **BridgeHostObject 保留 ServiceLocator** | COM 对象无法走 DI 构造，但改为构造时一次性获取 |
| **Entitys 新增 EventHub 依赖** | EventHub 是轻量级纯事件库，不引入重量级依赖 |
