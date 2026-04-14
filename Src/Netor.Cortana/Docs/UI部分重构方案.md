# Netor.Cortana UI 部分重构方案

> 本文档整理自技术方案讨论，记录关键决策点和可实施性分析。

> 历史说明：本文档形成于旧 WinForms/WinFormedge 阶段，内部关于 WinUI 3、Avalonia 不可行等判断已不再代表当前项目事实。当前主 UI 已切换到 Netor.Cortana.AvaloniaUI。

---

## 一、现有 UI 架构现状

当前 UI 层是 **"C# 宿主壳 + Chromium 网页"** 架构：

| 层 | 技术 | 职责 |
|---|---|---|
| 渲染层 | WinFormedge（基于 WebView2/Chromium） | 实际界面（HTML/CSS/JS） |
| 宿主层 | WinForms | 窗口容器、生命周期 |
| 桥接层 | `[ComVisible]` BridgeHostObject | JS ↔ C# 通信 |
| 窗口 | MainWindow / FloatWindow / SettingsWindow / WakeWordBubbleWindow | 4 种窗口形态 |

---

## 二、重构目标

支持 AOT 发布（Self-contained + Full Trimming，目标为 Native AOT），同时保留现有 Web 前端（HTML/CSS/JS）不动。

---

## 三、框架选型结论

### 推荐：WinUI 3 + WebView2

| 评估维度 | 结论 |
|---|---|
| AOT 程度 | Self-contained + Full Trimming 当前可用；Native AOT 因 WinRT COM 激活层为实验性（框架级限制，非用户代码） |
| WebView2 支持 | 原生一等公民，无需第三方包 |
| 透明/异形窗口 | `AppWindow` + Mica/Acrylic + Win32 扩展样式，原生支持 |
| 多窗口管理 | `AppWindow` 完整支持 |
| 系统集成 | Windows App SDK 1.4+ 含 TrayIcon，全面支持 |
| 本项目 XAML 复杂度 | 极低——每个窗口只有一个 `WebView2` 控件，Trimmer 无障碍 |

### 排除方案

- **WPF**：XAML 解析器深度依赖反射，Native AOT 不可用。
- **Avalonia**：WebView 支持第三方、成熟度低，迁移风险不可控。
- **Uno Platform**：纯 Windows 项目引入跨平台框架无必要。

---

## 四、JS ↔ C# 通信层：用 WebSocket 替代 BridgeHostObject

### 核心想法

```
现在：HTML/JS ←→ [COM BridgeHostObject] ←→ C# 服务
重构后：HTML/JS ←→ [WebSocket 双向] ←→ C# 服务
```

### 可行性

- ✅ 项目中 `WebSocketServer` 已存在，当前为单向推送（C# → JS）
- ✅ 扩展为双向即可完全覆盖 BridgeHostObject 的所有功能
- ✅ 完全消除 COM 依赖（AOT 最大障碍）
- ✅ 前端代码与宿主框架解耦，可独立在浏览器中测试

### 端口传递

端口号固定或写入配置文件。也可在 WebView2 页面加载前注入：

```csharp
await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
    $"window.__cortanaWsPort = {wsPort};"
);
```

此方式纯字符串注入，AOT 安全。

### 消息协议（双向）

```
JS → C#（命令）：{ "action": "sendMessage", "payload": { "text": "..." } }
C# → JS（事件）：{ "event": "ai_token", "data": { "text": "..." } }
```

使用 `System.Text.Json` + Source Generator 序列化，AOT 安全。

### SettingsWindow 请求-响应模式

原 BridgeHostObject 中同步返回数据的操作，改为带 `requestId` 的异步请求-响应消息对：

```
JS → C#：{ "action": "getProviders", "requestId": "abc123" }
C# → JS：{ "event": "response", "requestId": "abc123", "data": [...] }
```

### 与模块化重构的对齐

| 模块 | 职责 |
|---|---|
| `WebSocketServer` | 已在 Networks 层，扩展为双向 |
| `WebSocketEventRelay` | C# EventHub 事件 → JS 广播（已有设计） |
| `WebSocketCommandRouter` | JS 命令 → C# EventHub 事件发布（新增） |

---

## 五、FloatWindow 和 BubbleWindow 的处理方案

### 问题

每个 WebView2 实例 = 独立的 Chromium Renderer 进程 ≈ 25~40MB 常驻内存。  
这两个窗口都很轻，没必要承担 Renderer 进程开销。

### 结论：两个窗口都改为原生 XAML 实现

| 窗口 | XAML 实现 | 说明 |
|---|---|---|
| **FloatWindow**（悬浮球） | `Window → Grid → Ellipse → Image` | 圆形 + 头像，无动画 |
| **BubbleWindow**（字幕气泡） | `Window → Border → TextBlock` | 圆角容器 + 文字更新 |

- 视觉效果（圆形、圆角、毛玻璃）用 WinUI 3 基础控件即可实现
- 动效可去掉或减弱，不影响功能
- 文字内容更新通过代码直接赋值（`textBlock.Text = value`），无需任何绑定机制

---

## 六、AOT 风险汇总

| 检查项 | 风险 |
|---|---|
| 基础 XAML 控件（Border / TextBlock / Image / Ellipse） | ✅ 无风险（编译期生成类型元数据） |
| 使用 `x:Bind` 或代码直接赋值（不用 `{Binding}`） | ✅ 无风险 |
| 不使用 `Storyboard` 动画 | ✅ 无风险（Storyboard 属性路径是字符串才有风险） |
| 透明窗口 Win32 P/Invoke（用 `LibraryImport`） | ✅ 无风险（源生成器，编译期 marshaling） |
| WebSocket 通信（System.Text.Json Source Generator） | ✅ 无风险 |
| COM BridgeHostObject | ❌ 已完全移除 |
| WinUI 3 Native AOT（WinRT COM 激活） | ⚠️ 框架级限制，Trimming 模式完全满足需求 |

---

## 七、重构后的窗口架构

```
WinUI 3 宿主
  ├── MainWindow        → WebView2（主对话界面，WebSocket 双向通信）
  ├── SettingsWindow    → WebView2（设置界面，WebSocket 双向通信）
  ├── FloatWindow       → 纯 XAML（Ellipse + Image，无 WebView2）
  └── BubbleWindow      → 纯 XAML（Border + TextBlock，无 WebView2）
```

节省 2 个 Chromium Renderer 进程，约 **50~80MB 常驻内存**。

---

## 八、发布目标

| 发布模式 | 可行性 | 说明 |
|---|---|---|
| Self-contained + Full Trimming | ✅ 当前可用 | 消除目标机 .NET Runtime 依赖 |
| Native AOT | ⚠️ 实验性 | WinRT COM 激活层框架级限制，微软 .NET 10 路线图中 |

---

## 九、全项目第三方依赖 AOT 风险分析

### 风险总表

| 包 | 所在项目 | 风险级别 | Trimming 阻断 | 说明 |
|---|---|---|---|---|
| `WinFormedge` | 主程序 | ❌ 阻断 | 是 | WinForms 深度反射；**替换为 WinUI 3（本方案核心）** |
| `LiteDB` | Entitys / AI | ❌ 阻断 | 是 | 反射型 ORM，`Reflection.Emit` Native AOT 不支持；**已决定替换为 SQLite** |
| `OllamaSharp` | AI | ✅ 安全 | 否 | 内置 `JsonSourceGenerationContext`，所有模型类型均走 Source Generator |
| `ModelContextProtocol` | Plugin | ✅ 安全 | 否 | 微软官方 SDK，STJ Source Generator 驱动，工具元数据为 `JsonElement` |
| `Microsoft.Agents.AI` / `Microsoft.Extensions.AI` | AI / Plugin | ✅ 安全 | 否 | `AIFunctionFactory.Create` 无 `[RequiresUnreferencedCode]`，参数类型均为原始类型 |
| `Serilog` + `WriteTo.File` | 主程序 | ✅ 安全 | 否 | 用法简单（无 Enricher / Destructure），4.x 已改善 Trimming |
| `NAudio.WinMM` | Voice | ✅ 安全 | 否 | 纯 P/Invoke |
| `org.k2fsa.sherpa.onnx` | Voice | ✅ 安全 | 否 | 纯 P/Invoke |
| `Microsoft.Extensions.*` | 全部 | ✅ 安全 | 否 | .NET 10 官方库，完全 AOT 兼容 |
| `Netor.EventHub` | 多个 | ✅ 安全 | 否 | Source Generator 静态派发 |

### LiteDB → SQLite 迁移

**替换原因：** LiteDB 通过 `BsonMapper` + `Activator.CreateInstance` + `Expression.Compile` 做反射型 ORM，`Reflection.Emit` 在 Native AOT 下硬崩溃，Trimming 下实体属性可能被裁剪。

**替换方案：** `Microsoft.Data.Sqlite`（纯 P/Invoke，AOT 零风险），配合手写 reader 或 Dapper-AOT。

```csharp
// LiteDB（旧）
db.GetCollection<AgentEntity>("Agents").Insert(entity);

// SQLite（新）
connection.Execute("INSERT INTO Agents ...", parameters);
```

`CortanaDbContext` 类名保持不变，上层服务无感知替换。

### MAF 工具注册最佳实践

目前工具参数全为 `string`/原始类型，风险为零。若引入自定义参数类型，需显式传入 Source Generator 上下文：

```csharp
[JsonSerializable(typeof(MyRequest))]
internal partial class ToolJsonContext : JsonSerializerContext { }

var tool = AIFunctionFactory.Create(MyMethod,
    new AIFunctionFactoryOptions { SerializerOptions = ToolJsonContext.Default.Options });
```

---

## 十、插件通道架构与 AOT 升级路径

### 现有三通道设计

```
┌─────────────────────────────────────────────────────────────────┐
│  Cortana.exe（Self-contained + Trimming，含 JIT）               │
│                                                                 │
│  ┌─────────────────┐  ┌──────────────────┐  ┌──────────────┐  │
│  │  AOT 原生通道   │  │   MCP 通道       │  │ .NET JIT 通道│  │
│  │  NativePlugin   │  │  McpServerHost   │  │PluginLoad    │◄─┼── 普通 IL DLL
│  │  (P/Invoke)     │  │  (JSON-RPC)      │  │Context (ALC) │  │   无需 AOT
│  └─────────────────┘  └──────────────────┘  └──────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

**当前目标（Self-contained + Trimming）下三通道完全并存。** `.NET JIT 通道`的插件开发者无需任何 AOT 兼容，用普通类库项目实现 `IPlugin` 即可。

### 各通道插件开发者要求

| 通道 | 开发者要求 | 宿主依赖 |
|---|---|---|
| AOT 原生通道 | 必须 AOT 兼容（C++ / Native AOT .NET） | 纯 P/Invoke |
| MCP 通道 | 无限制（任意语言，只需实现 MCP 协议） | JSON-RPC over stdio/HTTP |
| .NET JIT 通道 | **无需 AOT，普通 .NET 类库即可** | 需宿主含 JIT（Trimming 模式） |

### Native AOT 宿主升级路径（长期）

若宿主未来升级为 Native AOT，`.NET JIT 通道`必须从同进程移出，改为 **Out-of-Process JIT Host**：

```
┌──────────────────────────────────────────────────────────────────┐
│  Cortana.exe（Native AOT）                                       │
│                                                                  │
│  AOT 通道（进程内）+ MCP 通道（进程内）                           │
│  .NET 通道 → IPC/WebSocket ─────────────────────────────────┐   │
└────────────────────────────────────────────────────────────────┘ │
                                                                    │
      ┌─────────────────────────────────────────┐                  │
      │  Cortana.JitHost.exe（Self-contained）  │◄─────────────────┘
      │  PluginLoadContext → 加载普通 IL 插件    │
      └─────────────────────────────────────────┘
```

**复用基础：** 现有 `Netor.Cortana.NativeHost.exe` 提供了完全对称的范式；`IChatTransport` / `WebSocketServer` 基础设施可直接复用为 IPC 层。**现阶段无需改动，按需升级。**
