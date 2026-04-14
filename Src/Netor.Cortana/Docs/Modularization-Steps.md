# Netor.Cortana 模块化重构 —— 实施步骤

> 本文档是 `Modularization-Design.md` 的配套执行手册。
> 每个步骤标注了**前置条件**、**具体操作**、**验证方式**，可逐步交给 Copilot 执行。

> 历史说明：本文档服务于旧 Netor.Cortana 主项目拆分阶段，很多步骤已完成、调整或被 AvaloniaUI 主线替代。执行前必须先和当前解决方案结构核对，不能直接照抄。

---

## 阶段 A：项目骨架配置（4个新项目已创建，需配置 TFM / NuGet / 项目引用）

> **前置条件**：解决方案中已存在空项目 `Netor.Cortana.AI`、`Netor.Cortana.Voice`、`Netor.Cortana.Plugin`、`Netor.Cortana.Networks`。
> 当前均为空 csproj，TFM 为 `net10.0`，无 NuGet 包和项目引用。

### A-1 配置 Netor.Cortana.Voice.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWindowsForms>True</UseWindowsForms>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="org.k2fsa.sherpa.onnx" Version="1.12.35" />
    <PackageReference Include="NAudio.WinMM" Version="2.3.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.5" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Netor.Cortana.Entitys\Netor.Cortana.Entitys.csproj" />
  </ItemGroup>
</Project>
```

**验证**：编译 Voice 项目通过（空项目无源文件即可）。

### A-2 配置 Netor.Cortana.AI.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWindowsForms>True</UseWindowsForms>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Agents.AI" Version="1.0.0" />
    <PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.0.0" />
    <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.0.0" />
    <PackageReference Include="Microsoft.Extensions.AI" Version="10.4.1" />
    <PackageReference Include="OllamaSharp" Version="5.4.25" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.5" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Netor.Cortana.Entitys\Netor.Cortana.Entitys.csproj" />
    <ProjectReference Include="..\Netor.Cortana.Plugin\Netor.Cortana.Plugin.csproj" />
  </ItemGroup>
</Project>
```

**验证**：编译 AI 项目通过。

### A-3 配置 Netor.Cortana.Plugin.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWindowsForms>True</UseWindowsForms>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ModelContextProtocol" Version="1.2.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.5" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Netor.Cortana.Entitys\Netor.Cortana.Entitys.csproj" />
    <ProjectReference Include="..\Plugins\Netor.Cortana.Plugin.Abstractions\Netor.Cortana.Plugin.Abstractions.csproj" />
  </ItemGroup>
</Project>
```

**验证**：编译 Plugin 项目通过。

### A-4 配置 Netor.Cortana.Networks.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.5" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.5" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Netor.Cortana.Entitys\Netor.Cortana.Entitys.csproj" />
  </ItemGroup>
</Project>
```

**验证**：编译 Networks 项目通过。

### A-5 UI 壳添加对4个新项目的引用

在 `Netor.Cortana.csproj` 的 `<ProjectReference>` 组中追加：

```xml
<ProjectReference Include="..\Netor.Cortana.AI\Netor.Cortana.AI.csproj" />
<ProjectReference Include="..\Netor.Cortana.Voice\Netor.Cortana.Voice.csproj" />
<ProjectReference Include="..\Netor.Cortana.Plugin\Netor.Cortana.Plugin.csproj" />
<ProjectReference Include="..\Netor.Cortana.Networks\Netor.Cortana.Networks.csproj" />
```

### A-6 全量编译验证

```bash
dotnet build Netor.Cortana.sln
```

**验证**：0 error。所有项目均为空类库 + 正确的依赖图。

---

## 阶段 B：Entitys 层扩展（事件定义 + 接口契约）

> **前置条件**：阶段 A 完成，所有项目编译通过。

### B-1 Entitys 添加 EventHub NuGet 依赖

在 `Netor.Cortana.Entitys.csproj` 中追加：

```xml
<PackageReference Include="Netor.EventHub" Version="1.2.5" />
```

### B-2 迁入 Events.cs

1. 将 `Src/Netor.Cortana/Services/Events.cs` **复制**到 `Src/Netor.Cortana.Entitys/Events.cs`
2. 修改命名空间：`Netor.Cortana.Services` → `Netor.Cortana.Entitys`
3. 修改访问修饰符：`internal static class Events` → `public static class Events`，所有 `internal static` 字段 → `public static`
4. 添加缺少的 using：`using Netor.EventHub;`、`using EventArgs = Netor.EventHub.EventArgs;`

### B-3 新增 TtsEnqueue / TtsFinish 事件

在 `Events.cs` 中追加：

```csharp
// ──────── AI → Voice 事件驱动 ────────

/// <summary>AI 断句完成，请求 TTS 合成并播放该句文本。</summary>
public static TtsEnqueueEvent OnTtsEnqueue = new("voice.tts.enqueue");

/// <summary>AI 推理完成，没有后续文本了。Voice 合成完剩余队列即可。</summary>
public static VoiceSignalEvent OnTtsFinish = new("voice.tts.finish");
```

在事件类型区追加：

```csharp
/// <summary>TTS 入队事件（携带句子文本）</summary>
public record TtsEnqueueEvent(string Eventid) : EventID<TtsEnqueueArgs>(Eventid);

/// <summary>TTS 入队事件参数</summary>
/// <param name="Sentence">断句后的文本</param>
public record TtsEnqueueArgs(string Sentence) : EventArgs;
```

### B-4 创建 IAppPaths.cs

在 `Src/Netor.Cortana.Entitys/IAppPaths.cs` 创建：

```csharp
namespace Netor.Cortana.Entitys;

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

### B-5 创建 IWindowController.cs

在 `Src/Netor.Cortana.Entitys/IWindowController.cs` 创建：

```csharp
namespace Netor.Cortana.Entitys;

/// <summary>
/// 窗口控制契约，解耦业务层对 UI 窗口的直接依赖。
/// </summary>
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

### B-6 编译验证

```bash
dotnet build Netor.Cortana.Entitys
```

**验证**：Entitys 项目编译通过，Events / IAppPaths / IWindowController 均可被其他项目引用。

> **注意**：此时主项目 `Netor.Cortana` 中 `Services/Events.cs` 仍保留，会与 Entitys 中的冲突。
> 暂不删除，等阶段 G UI壳瘦身时统一处理。可在主项目 `Events.cs` 中临时用 `extern alias` 或 `#if` 隔离。
> 或者更简单的做法：阶段 B 完成后立即删除主项目的 `Services/Events.cs` 并更新 `using`。

---

## 阶段 C：Voice 层迁移（3个文件 + 事件订阅改造）

> **前置条件**：阶段 B 完成，Entitys 中已有 Events / IAppPaths。

### C-1 迁入 WakeWordService.cs

1. **复制** `Src/Netor.Cortana/Services/WakeWordService.cs` → `Src/Netor.Cortana.Voice/WakeWordService.cs`
2. 修改命名空间：`Netor.Cortana.Services` → `Netor.Cortana.Voice`
3. 访问修饰符：`internal sealed class` → `public sealed class`
4. `App.UserDataDirectory` → 构造函数注入 `IAppPaths`，改用 `appPaths.UserDataDirectory`
5. 事件引用：`Events.OnWakeWordDetected` 等已在 Entitys 中，确认 using

### C-2 迁入 SpeechRecognitionService.cs

1. **复制** → `Src/Netor.Cortana.Voice/SpeechRecognitionService.cs`
2. 同 C-1 的修改模式：命名空间、访问修饰符、`IAppPaths` 注入
3. 事件发布：`Events.OnSttPartial` / `OnSttFinal` / `OnSttStopped` 保持不变（已在 Entitys）

### C-3 迁入 TextToSpeechService.cs

1. **复制** → `Src/Netor.Cortana.Voice/TextToSpeechService.cs`
2. 命名空间 + 访问修饰符修改
3. `App.UserDataDirectory` → `IAppPaths` 注入
4. `AppSettings` 引用 → 如 AppSettings 尚未迁入 AI 层，暂用接口或直接传入配置值（后续 F 阶段处理）

### C-4 TextToSpeechService 新增事件订阅（核心改造）

在 `TextToSpeechService` 中添加事件订阅逻辑：

```csharp
/// <summary>
/// 订阅 AI 层的 TTS 事件，实现事件驱动的合成管线。
/// 应在 DI 初始化后调用一次。
/// </summary>
public void SubscribeTtsEvents(ISubscriber subscriber)
{
    subscriber.Subscribe<TtsEnqueueArgs>(Events.OnTtsEnqueue, async (args, _) =>
    {
        await EnqueueTextAsync(args.Sentence);
        return false;
    });

    subscriber.Subscribe<VoiceSignalArgs>(Events.OnTtsFinish, async (_, _) =>
    {
        await FinishAndWaitAsync();
        return false;
    });
}
```

> 或将订阅写在构造函数中（取决于 ISubscriber 的生命周期）。

### C-5 创建 VoiceServiceExtensions.cs（DI 扩展方法）

在 `Src/Netor.Cortana.Voice/VoiceServiceExtensions.cs` 创建：

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Netor.Cortana.Voice;

public static class VoiceServiceExtensions
{
    public static IServiceCollection AddCortanaVoice(this IServiceCollection services)
    {
        services.AddSingleton<WakeWordService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<WakeWordService>());
        services.AddSingleton<SpeechRecognitionService>();
        services.AddSingleton<TextToSpeechService>();
        return services;
    }
}
```

### C-6 编译验证

```bash
dotnet build Netor.Cortana.Voice
```

**验证**：Voice 项目编译通过。3个服务 + DI 扩展方法 + 事件订阅就绪。

---

## 阶段 D：Plugin 层迁移（插件加载框架 + 内置桌面 Provider）

> **前置条件**：阶段 B 完成。Plugin 层与 Voice / AI 无依赖关系，可与 C 并行。

### D-1 迁入插件加载框架核心文件

复制以下文件到 `Src/Netor.Cortana.Plugin/`：

| 原路径 | 新路径 |
|--------|--------|
| `Plugins/PluginLoader.cs` | `Plugin/PluginLoader.cs` |
| `Plugins/PluginContext.cs` | `Plugin/PluginContext.cs` |
| `Plugins/PluginContextProvider.cs` | `Plugin/PluginContextProvider.cs` |
| `Plugins/PluginManifest.cs` | `Plugin/PluginManifest.cs` |
| `Plugins/Mcp/McpContextProvider.cs` | `Plugin/Mcp/McpContextProvider.cs` |
| `Plugins/Mcp/McpServerHost.cs` | `Plugin/Mcp/McpServerHost.cs` |
| `Plugins/Dotnet/DotNetPluginHost.cs` | `Plugin/Dotnet/DotNetPluginHost.cs` |
| `Plugins/Dotnet/PluginLoadContext.cs` | `Plugin/Dotnet/PluginLoadContext.cs` |
| `Plugins/Native/NativePluginHost.cs` | `Plugin/Native/NativePluginHost.cs` |
| `Plugins/Native/NativePluginWrapper.cs` | `Plugin/Native/NativePluginWrapper.cs` |
| `Plugins/Native/NativePluginInfo.cs` | `Plugin/Native/NativePluginInfo.cs` |
| `Plugins/Native/NativeHostProtocol.cs` | `Plugin/Native/NativeHostProtocol.cs` |

修改所有文件：
- 命名空间 → `Netor.Cortana.Plugin`（子目录用 `Netor.Cortana.Plugin.Mcp` 等）
- `internal` → `public`
- `App.Services` → 构造函数注入
- `App.WorkspaceDirectory` / `App.UserDataDirectory` → `IAppPaths`

### D-2 迁入桌面控制内置 Provider

复制到 `Src/Netor.Cortana.Plugin/BuiltIn/`：

| 原路径 | 新路径 |
|--------|--------|
| `Providers/PowerShell/PowerShellProvider.cs` | `Plugin/BuiltIn/PowerShell/PowerShellProvider.cs` |
| `Providers/PowerShell/PowerShellExecutor.cs` | `Plugin/BuiltIn/PowerShell/PowerShellExecutor.cs` |
| `Providers/PowerShell/PowerShellOutputBridge.cs` | `Plugin/BuiltIn/PowerShell/PowerShellOutputBridge.cs` |
| `Providers/PowerShell/SessionRegistry.cs` | `Plugin/BuiltIn/PowerShell/SessionRegistry.cs` |
| `Providers/PowerShell/ExecutionSession.cs` | `Plugin/BuiltIn/PowerShell/ExecutionSession.cs` |
| `Providers/PowerShellProvider.cs`（根目录的） | `Plugin/BuiltIn/PowerShell/PowerShellProvider.cs`（合并或选正确的） |
| `Providers/DesktopControl/WindowManagement/WindowManager.cs` | `Plugin/BuiltIn/WindowManagement/WindowManager.cs` |
| `Providers/DesktopControl/WindowManagement/WindowManagerProvider.cs` | `Plugin/BuiltIn/WindowManagement/WindowManagerProvider.cs` |
| `Providers/DesktopControl/WindowManagement/WindowInfo.cs` | `Plugin/BuiltIn/WindowManagement/WindowInfo.cs` |
| `Providers/DesktopControl/FileBrowser/FileBrowser.cs` | `Plugin/BuiltIn/FileBrowser/FileBrowser.cs` |
| `Providers/DesktopControl/FileBrowser/FileBrowserProvider.cs` | `Plugin/BuiltIn/FileBrowser/FileBrowserProvider.cs` |
| `Providers/DesktopControl/FileBrowser/FileItemInfo.cs` | `Plugin/BuiltIn/FileBrowser/FileItemInfo.cs` |
| `Providers/DesktopControl/ApplicationLauncher/ApplicationLauncher.cs` | `Plugin/BuiltIn/ApplicationLauncher/ApplicationLauncher.cs` |
| `Providers/DesktopControl/ApplicationLauncher/ApplicationLauncherProvider.cs` | `Plugin/BuiltIn/ApplicationLauncher/ApplicationLauncherProvider.cs` |
| `Providers/DesktopControl/ApplicationLauncher/ApplicationInfo.cs` | `Plugin/BuiltIn/ApplicationLauncher/ApplicationInfo.cs` |

修改：命名空间统一、访问修饰符、`IAppPaths` 注入。

### D-3 创建 PluginServiceExtensions.cs

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Netor.Cortana.Plugin;

public static class PluginServiceExtensions
{
    public static IServiceCollection AddCortanaPlugin(this IServiceCollection services)
    {
        services.AddSingleton<PluginLoader>();
        // 内置 Provider（后续 AOT 插件化后移除）
        services.AddSingleton<AIContextProvider, PowerShellProvider>();
        services.AddSingleton<AIContextProvider, WindowManagerProvider>();
        services.AddSingleton<AIContextProvider, FileBrowserProvider>();
        services.AddSingleton<AIContextProvider, ApplicationLauncherProvider>();
        // 辅助服务
        services.AddSingleton<PowerShellExecutor>();
        services.AddSingleton<PowerShellOutputBridge>();
        services.AddSingleton<SessionRegistry>();
        services.AddSingleton<WindowManager>();
        services.AddSingleton<FileBrowser>();
        services.AddSingleton<ApplicationLauncher>();
        return services;
    }
}
```

### D-4 编译验证

```bash
dotnet build Netor.Cortana.Plugin
```

**验证**：Plugin 项目编译通过。

---

## 阶段 E：Networks 层迁移（WebSocket + 事件中转）

> **前置条件**：阶段 B 完成。Networks 仅依赖 Entitys，可与 C/D 并行。

### E-1 迁入 WebSocketServer.cs

1. **复制** `Src/Netor.Cortana/Services/WebSocketServer.cs` → `Src/Netor.Cortana.Networks/WebSocketServer.cs`
2. 命名空间：`Netor.Cortana.Services` → `Netor.Cortana.Networks`
3. 访问修饰符：`internal` → `public`
4. 移除对 AI/Voice 层的直接引用（如有）

### E-2 创建 WebSocketEventRelay.cs

在 `Src/Netor.Cortana.Networks/WebSocketEventRelay.cs` 创建：

```csharp
using Netor.Cortana.Entitys;
using Netor.EventHub.Interfances;

namespace Netor.Cortana.Networks;

/// <summary>
/// 事件中转站：订阅 EventHub 事件，通过 WebSocket 广播到所有前端客户端。
/// 取代原来 AI 层直接调用 WebSocketServer.BroadcastAsync 的耦合方式。
/// </summary>
public sealed class WebSocketEventRelay(
    WebSocketServer server,
    ISubscriber subscriber)
{
    public void Start()
    {
        // STT
        subscriber.Subscribe<VoiceTextArgs>(Events.OnSttPartial, async (args, _) =>
        {
            await server.BroadcastAsync("stt_partial", args.Text);
            return false;
        });

        subscriber.Subscribe<VoiceTextArgs>(Events.OnSttFinal, async (args, _) =>
        {
            await server.BroadcastAsync("stt_final", args.Text);
            return false;
        });

        subscriber.Subscribe<VoiceSignalArgs>(Events.OnSttStopped, async (_, _) =>
        {
            await server.BroadcastAsync("stt_stopped", "");
            return false;
        });

        // TTS
        subscriber.Subscribe<VoiceSignalArgs>(Events.OnTtsStarted, async (_, _) =>
        {
            await server.BroadcastAsync("tts_started", "");
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

        // Chat
        subscriber.Subscribe<VoiceSignalArgs>(Events.OnChatCompleted, async (_, _) =>
        {
            await server.BroadcastAsync("chat_completed", "");
            return false;
        });

        // WakeWord
        subscriber.Subscribe<VoiceSignalArgs>(Events.OnWakeWordDetected, async (_, _) =>
        {
            await server.BroadcastAsync("wakeword_detected", "");
            return false;
        });
    }
}
```

### E-3 创建 NetworkServiceExtensions.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Netor.Cortana.Networks;

public static class NetworkServiceExtensions
{
    public static IServiceCollection AddCortanaNetworks(this IServiceCollection services)
    {
        services.AddSingleton<WebSocketServer>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<WebSocketServer>());
        services.AddSingleton<WebSocketEventRelay>();
        return services;
    }
}
```

### E-4 编译验证

```bash
dotnet build Netor.Cortana.Networks
```

**验证**：Networks 项目编译通过。

---

## 阶段 F：AI 层迁移（最复杂：事件化 + 消除 App.Services）

> **前置条件**：阶段 B（Entitys）+ 阶段 D（Plugin，因 AIAgentFactory 引用 Provider）完成。

### F-1 迁入 AppSettings.cs

1. **复制** `Services/AppSettings.cs` → `Src/Netor.Cortana.AI/AppSettings.cs`
2. 命名空间 → `Netor.Cortana.AI`
3. 访问修饰符 → `public`

### F-2 迁入 AIAgentFactory.cs

1. **复制** → `Src/Netor.Cortana.AI/AIAgentFactory.cs`
2. 命名空间 → `Netor.Cortana.AI`
3. 消除 `App.Services.GetRequiredService<>()` 6处：
   - 改为构造函数注入所有需要的 Provider
   - 使用 `IEnumerable<AIContextProvider>` 批量注入插件 Provider
4. `App.WorkspaceDirectory` → `IAppPaths`

### F-3 迁入 AiChatService.cs

1. **复制** → `Src/Netor.Cortana.AI/AiChatService.cs`
2. 命名空间 → `Netor.Cortana.AI`
3. 消除 `App.Services`、`App.CancellationTokenSource`
4. WebSocket 直接调用 → 改为事件发布（由 Networks 层订阅推送）

### F-4 迁入 AiModelFetcherService.cs

1. **复制** → `Src/Netor.Cortana.AI/AiModelFetcherService.cs`
2. 命名空间、访问修饰符修改
3. 无 `App.xxx` 依赖，改动最小

### F-5 迁入 VoiceChatService.cs（核心改造）

1. **复制** → `Src/Netor.Cortana.AI/VoiceChatService.cs`
2. 命名空间 → `Netor.Cortana.AI`

**关键改造——去掉 TTS 直接调用**：

```csharp
// 改造前（StreamAiResponseAsync 中）：
await ttsService.EnqueueTextAsync(sentence, cancellationToken);
// ...
await ttsService.FinishAndWaitAsync(cancellationToken);

// 改造后：
publisher.Publish(Events.OnTtsEnqueue, new TtsEnqueueArgs(sentence));
// ...
publisher.Publish(Events.OnTtsFinish, new VoiceSignalArgs());
```

**关键改造——去掉 WebSocket 直接调用**：

```csharp
// 改造前：
await wsServer.BroadcastAsync("voice_user", userText, token);
await wsServer.BroadcastAsync("voice_token", chunk.Text, cancellationToken);
await wsServer.BroadcastAsync("voice_done", _sessionId, cancellationToken);

// 改造后：通过事件发布，由 Networks 层 WebSocketEventRelay 订阅推送
publisher.Publish(Events.OnVoiceUser, new VoiceTextArgs(userText));
publisher.Publish(Events.OnAiToken, new VoiceTextArgs(chunk.Text));
publisher.Publish(Events.OnChatCompleted, new VoiceSignalArgs());
```

> 注：可能需要在 Events.cs 中新增 `OnVoiceUser` 和 `OnAiToken` 事件。

**关键改造——去掉构造函数中的 TTS/WebSocket 注入**：

```csharp
// 改造前：
internal sealed class VoiceChatService(
    ILogger<VoiceChatService> logger,
    TextToSpeechService ttsService,      // ← 删除
    WebSocketServer wsServer,            // ← 删除
    AIAgentFactory agentFactory,
    CortanaDbContext dbContext,
    IPublisher publisher,
    ISubscriber subscriber) : IDisposable

// 改造后：
public sealed class VoiceChatService(
    ILogger<VoiceChatService> logger,
    AIAgentFactory agentFactory,
    CortanaDbContext dbContext,
    IWindowController windowController,  // ← 新增
    IPublisher publisher,
    ISubscriber subscriber) : IDisposable
```

**关键改造——窗口可见性判断**：

```csharp
// 改造前：
var skipTts = IsMainWindowVisible();  // 内部调用 Application.OpenForms[0].Visible

// 改造后：
var skipTts = windowController.IsMainWindowVisible();
```

**关键改造——消除 App.Services**：

`LoadDefaults()` 中 3 处 `App.Services.GetRequiredService<>()` → 构造函数注入：

```csharp
// 改造前：
var providerService = App.Services.GetRequiredService<AiProviderService>();
var agentService = App.Services.GetRequiredService<AgentService>();
var modelService = App.Services.GetRequiredService<AiModelService>();

// 改造后（构造函数注入）：
public sealed class VoiceChatService(
    ...
    AiProviderService providerService,
    AgentService agentService,
    AiModelService modelService,
    ...) : IDisposable
```

### F-6 迁入 ChatHistoryDataProvider.cs

1. **复制** → `Src/Netor.Cortana.AI/Providers/ChatHistoryDataProvider.cs`
2. 命名空间 → `Netor.Cortana.AI.Providers`
3. 消除 `App.Services` 残余（1处）

### F-7 迁入 FileMemoryProvider.cs

1. **复制** → `Src/Netor.Cortana.AI/Providers/FileMemoryProvider.cs`
2. `App.WorkspaceDirectory` → `IAppPaths`

### F-8 创建 AIServiceExtensions.cs

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Netor.Cortana.AI;

public static class AIServiceExtensions
{
    public static IServiceCollection AddCortanaAI(this IServiceCollection services)
    {
        services.AddSingleton<AIAgentFactory>();
        services.AddSingleton<ChatHistoryDataProvider>();
        services.AddSingleton<FileMemoryProvider>();
        services.AddSingleton<AiChatService>();
        services.AddSingleton<VoiceChatService>();
        services.AddTransient<AiModelFetcherService>();
        return services;
    }
}
```

### F-9 编译验证

```bash
dotnet build Netor.Cortana.AI
```

**验证**：AI 项目编译通过。VoiceChatService 不再引用 TextToSpeechService 和 WebSocketServer。

---

## 阶段 G：UI 壳瘦身

> **前置条件**：阶段 C/D/E/F 全部完成，4个新项目各自编译通过。

### G-1 删除已迁移到 Voice 层的文件

从 `Src/Netor.Cortana/` 删除：

- `Services/WakeWordService.cs`
- `Services/SpeechRecognitionService.cs`
- `Services/TextToSpeechService.cs`

### G-2 删除已迁移到 AI 层的文件

- `Services/AiChatService.cs`
- `Services/AIAgentFactory.cs`
- `Services/AiModelFetcherService.cs`
- `Services/VoiceChatService.cs`
- `Services/AppSettings.cs`
- `Providers/ChatHistoryDataProvider.cs`
- `Providers/FileMemoryProvider.cs`

### G-3 删除已迁移到 Plugin 层的文件

- `Plugins/PluginLoader.cs`
- `Plugins/PluginContext.cs`
- `Plugins/PluginContextProvider.cs`
- `Plugins/PluginManifest.cs`
- `Plugins/Mcp/McpContextProvider.cs`
- `Plugins/Mcp/McpServerHost.cs`
- `Plugins/Dotnet/DotNetPluginHost.cs`
- `Plugins/Dotnet/PluginLoadContext.cs`
- `Plugins/Native/NativePluginHost.cs`
- `Plugins/Native/NativePluginWrapper.cs`
- `Plugins/Native/NativePluginInfo.cs`
- `Plugins/Native/NativeHostProtocol.cs`
- `Providers/PowerShellProvider.cs`
- `Providers/PowerShell/` 整个目录
- `Providers/DesktopControl/` 整个目录

### G-4 删除已迁移到 Networks 层的文件

- `Services/WebSocketServer.cs`

### G-5 删除已迁移到 Entitys 层的文件

- `Services/Events.cs`

### G-6 App.cs 实现 IAppPaths + IWindowController

```csharp
// App.cs 追加接口实现
public partial class App : AppStartup, IAppPaths, IWindowController
{
    // IAppPaths
    public string WorkspaceDirectory => App.WorkspaceDirectory; // 已有静态属性
    public string UserDataDirectory => App.UserDataDirectory;
    public string PluginDirectory => Path.Combine(UserDataDirectory, "plugins");

    // IWindowController
    public void ShowMainWindow() => MainWindow?.Show();
    public void HideMainWindow() => MainWindow?.Hide();
    public void ShowSettingsWindow() { /* 现有逻辑 */ }
    public void ShowFloatWindow() { /* 现有逻辑 */ }
    public void MoveFloatWindow(int x, int y) { /* 现有逻辑 */ }
    public bool IsMainWindowVisible() => MainWindow?.Visible ?? false;
}
```

### G-7 Program.cs 改用 AddCortanaXxx() 扩展方法

改造 `ConfiguarServices` 方法，用模块扩展方法替换逐个注册：

```csharp
private static void ConfiguarServices(IServiceCollection services)
{
    var appSettings = EmbeddedConfigurationExtensions
        .LoadEmbeddedJson<AppSettings>("Netor.Cortana.appsettings.json");

    services
        .AddSingleton(appSettings)
        .AddLogging(/* ... 保持不变 ... */)
        .AddEventHub()
        .AddHttpClient()
        // ── 模块注册（一行一个） ──
        .AddCortanaVoice()
        .AddCortanaAI()
        .AddCortanaPlugin()
        .AddCortanaNetworks()
        // ── UI 壳自身 ──
        .AddSingleton<IAppPaths>(sp => /* App 实例 */)
        .AddSingleton<IWindowController>(sp => /* App 实例 */)
        .AddSingleton<MainWindow>()
        .AddSingleton<FloatWindow>()
        .AddSingleton<WakeWordBubbleWindow>()
        .AddSingleton<SettingsWindow>()
        // 数据库
        .AddSingleton<CortanaDbContext>()
        .AddTransient<SystemSettingsService>()
        .AddTransient<AgentService>()
        .AddTransient<AiProviderService>()
        .AddTransient<AiModelService>()
        .AddTransient<ChatMessageService>()
        .AddTransient<McpServerService>()
        // CortanaProvider 保留在 UI 壳
        .AddSingleton<CortanaProvider>();
}
```

> 注意：删除了原来逐个注册的 Voice/AI/Plugin/Networks 服务行。

### G-8 更新 globalusing.cs

删除已不需要的命名空间，添加新模块命名空间：

```csharp
// 删除（文件已迁走）：
// global using Netor.Cortana.Providers.PowerShell;
// global using Netor.Cortana.Plugins;

// 添加：
global using Netor.Cortana.AI;
global using Netor.Cortana.Voice;
global using Netor.Cortana.Plugin;
global using Netor.Cortana.Networks;
```

### G-9 更新 Netor.Cortana.csproj

移除已迁到子项目的 NuGet 包：

```xml
<!-- 删除以下行（已迁到 Voice） -->
<PackageReference Include="org.k2fsa.sherpa.onnx" ... />
<PackageReference Include="NAudio.WinMM" ... />

<!-- 删除以下行（已迁到 AI） -->
<PackageReference Include="Microsoft.Agents.AI" ... />
<PackageReference Include="Microsoft.Agents.AI.OpenAI" ... />
<PackageReference Include="Microsoft.Agents.AI.Workflows" ... />
<PackageReference Include="Microsoft.Extensions.AI" ... />
<PackageReference Include="OllamaSharp" ... />

<!-- 删除以下行（已迁到 Plugin） -->
<PackageReference Include="ModelContextProtocol" ... />
```

> 保留 `Netor.EventHub`（UI 壳仍需事件订阅）、`WinFormedge`、`Serilog`、`Microsoft.Extensions.*` 基础包。

### G-10 编译验证

```bash
dotnet build Netor.Cortana
```

**验证**：UI 壳编译通过。此时会出现大量 `using` 报错，逐一修复。

---

## 阶段 H：全量编译 + 集成验证

> **前置条件**：阶段 G 完成，UI 壳编译通过。

### H-1 全量编译

```bash
dotnet build Netor.Cortana.sln
```

逐一修复剩余编译错误，常见类型：

| 错误类型 | 原因 | 修复 |
|---------|------|------|
| CS0246 找不到类型 | 命名空间变更 | 添加 `using Netor.Cortana.AI;` 等 |
| CS0122 访问性不足 | 迁移后仍为 internal | 改为 public |
| CS0103 名称不存在 | `App.Services` 残留 | 改为构造函数注入 |
| CS0234 命名空间不包含 | Events 搬家了 | `using Netor.Cortana.Entitys;` |

### H-2 事件流集成验证

启动应用后验证以下事件链路：

**链路 1：唤醒词 → STT → AI → TTS → 字幕**
1. 说出唤醒词 → Voice 发布 `OnWakeWordDetected` ✓
2. STT 识别 → Voice 发布 `OnSttPartial` / `OnSttFinal` ✓
3. AI 推理断句 → AI 发布 `OnTtsEnqueue`（每句） ✓
4. AI 推理结束 → AI 发布 `OnTtsFinish` ✓
5. TTS 订阅合成播放 → Voice 发布 `OnTtsSubtitle`（每句字幕） ✓
6. TTS 全部播放完 → Voice 发布 `OnTtsCompleted` ✓

**链路 2：WebSocket 事件中转**
1. Networks 的 `WebSocketEventRelay` 订阅上述所有事件 ✓
2. 前端 WebSocket 收到 `tts_subtitle` 并显示字幕 ✓
3. 前端收到 `tts_completed` 并结束对话 ✓

**链路 3：MainWindow 可见时跳过 TTS**
1. `VoiceChatService` 通过 `IWindowController.IsMainWindowVisible()` 判断 ✓
2. 可见时不发布 `OnTtsEnqueue`，改为发布 AI token 事件 ✓

### H-3 回归验证清单

| 功能 | 验证方式 |
|------|---------|
| 语音唤醒 | 说唤醒词，观察 WakeWordBubbleWindow 弹出 |
| 语音识别 | 说话后字幕显示识别文本 |
| AI 语音对话 | 完整问答，TTS 播放回复 |
| AI 文字对话 | MainWindow 中输入文字，AI 回复显示 |
| WebSocket 推送 | 打开前端页面，验证实时消息 |
| 字幕同步 | TTS 播放时字幕逐句跟随 |
| 设置页面 | 修改 TTS 语速等参数生效 |
| 插件加载 | PluginLoader 正常扫描加载 |

### H-4 提交

```bash
git add -A
git commit -m "refactor: 模块化重构 — AI/Voice/Plugin/Networks 拆分完成"
git push origin master
```

---

## 附录：关键注意事项

### 迁移顺序原则

```
Entitys（基础）→ Voice（最独立）→ Plugin（文件多改动少）
    → Networks（最简单）→ AI（最复杂）→ UI壳（收尾）
```

### 每阶段必做

1. **复制而非移动**：先复制到新项目，修改编译通过后，再在阶段 G 统一删除原文件
2. **编译验证**：每阶段结束后编译当前项目，不等到最后
3. **不改业务逻辑**：仅做文件迁移 + 命名空间 + 访问修饰符 + DI 注入改造

### AppSettings 归属

`AppSettings.cs` 迁入 AI 层后，UI 壳仍需在 `Program.cs` 中加载嵌入资源中的 `appsettings.json` 并注册到 DI。
AI 层通过 DI 注入获取 `AppSettings` 实例，而非自行加载。

### 模型文件 Content 配置

SherpaOnnx 模型文件的 MSBuild `<Content>` 配置**保留在 UI 壳 csproj**：

```xml
<!-- 这些 Content 项不动，仍在 Netor.Cortana.csproj 中 -->
<Content Include="Models\vits-melo-tts-zh_en\**\*">...</Content>
<Content Include="Models\KWS\**\*">...</Content>
<Content Include="Models\STT\**\*">...</Content>
```

Voice 层只通过 `IAppPaths.UserDataDirectory` 读取模型路径，不关心模型文件如何打包。
