# SystemSettings 系统设置统一管理方案

> 文档版本：v1.0  
> 创建日期：2025-07-14  
> 状态：方案设计

> 历史说明：本文档是旧阶段的系统设置设计稿，正文中关于旧 WinForms 主项目、旧配置散落位置和 LiteDB 的表述可能与当前实现不一致。当前实现请优先对照 Entitys、AvaloniaUI 和最新 README。

---

## 一、背景与目标

当前系统存在配置信息散落在多处的问题，维护困难且无法在运行时动态调整：

| 散落位置 | 配置项 | 问题 |
|---------|--------|------|
| `appsettings.json`（嵌入资源） | SherpaOnnx 参数、TTS 语速 | 编译期固定，修改需重新发布 |
| `ChatHistoryDataProvider.cs` 硬编码 | `MaxContentLength=7500`、`MaxContentCount=15` | 无法动态调整 |
| `App.Startup.cs` | `WorkspaceDirectory` 默认值 | 每次启动重置，无持久化 |
| `Program.cs` | `Domain="cortana.me"`、`Scheme="https"` | 硬编码在启动入口 |
| `TextToSpeechService` | TTS Speed 从 AppSettings 读取 | 嵌入资源无法修改 |
| `WakeWordService` | KWS 灵敏度参数 | 嵌入资源无法修改 |
| `SpeechRecognitionService` | STT 静音超时参数 | 嵌入资源无法修改 |

**目标：** 新建 `SystemSettingsEntity` 键值对表，将所有通用配置统一存储到 SQLite 持久化层，支持运行时读写和界面管理。

---

## 二、SystemSettingsEntity 实体设计

在 `Netor.Cortana.Entitys` 项目中新建实体类，采用 **键值对** 设计：

```csharp
// Src/Netor.Cortana.Entitys/SystemSettingsEntity.cs

/// <summary>
/// 系统设置键值对实体。
/// Id 为设置键名（如 "SherpaOnnx.KeywordsThreshold"），Value 为 JSON 字符串值。
/// </summary>
public class SystemSettingsEntity : BaseEntity
{
    // Id 继承自 BaseEntity，作为设置的唯一键名
    // 例如: "SherpaOnnx.KeywordsThreshold", "Tts.Speed"

    /// <summary>
    /// 设置分组名称，用于界面分类展示。
    /// 例如: "语音唤醒", "语音识别", "语音合成", "对话历史", "系统"
    /// </summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// 设置项的中文显示名称。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 设置项描述/说明。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 设置值（JSON 字符串存储，支持 string/int/float/bool）。
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 默认值，用于重置。
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// 值类型标识：string / int / float / bool
    /// </summary>
    public string ValueType { get; set; } = "string";

    /// <summary>
    /// 排序权重，界面展示用。
    /// </summary>
    public int SortOrder { get; set; }
}
```

### 键名命名规范

采用 **分组.配置项** 的点分格式，与 appsettings.json 路径一一对应：

| 分组 | 键名 | 值类型 | 默认值 | 说明 |
|------|------|--------|--------|------|
| **语音唤醒** | `SherpaOnnx.KeywordsThreshold` | float | `0.01` | 唤醒词灵敏度阈值 |
| | `SherpaOnnx.KeywordsScore` | float | `9.0` | 唤醒词增强分数 |
| | `SherpaOnnx.NumTrailingBlanks` | int | `1` | 尾部空白帧数 |
| **语音识别** | `SherpaOnnx.Rule1MinTrailingSilence` | float | `5.0` | 无语音静音超时(秒) |
| | `SherpaOnnx.Rule2MinTrailingSilence` | float | `2.0` | 说话停顿超时(秒) |
| | `SherpaOnnx.Rule3MinUtteranceLength` | float | `30.0` | 单次语音最大时长(秒) |
| | `SherpaOnnx.RecognitionTimeoutSeconds` | float | `5.0` | 识别空闲超时(秒) |
| **语音合成** | `Tts.Speed` | float | `1.0` | TTS 语速倍率 |
| **对话历史** | `ChatHistory.MaxContentLength` | int | `7500` | 最大上下文字符数 |
| | `ChatHistory.MaxContentCount` | int | `15` | 最大保留消息条数 |
| **系统** | `System.WorkspaceDirectory` | string | (UserDataDirectory) | 当前工作目录 |

---

## 三、SystemSettingsService 服务层设计

在 `Netor.Cortana.Entitys/Services/` 下新建服务类：

```csharp
// Src/Netor.Cortana.Entitys/Services/SystemSettingsService.cs

public sealed class SystemSettingsService
{
    private readonly CortanaDbContext _db;

    public SystemSettingsService(CortanaDbContext db) { _db = db; }

    /// <summary> 获取指定键的设置值 </summary>
    public string? GetValue(string key);

    /// <summary> 获取指定键的设置值（带默认值）</summary>
    public string GetValue(string key, string defaultValue);

    /// <summary> 获取强类型值 </summary>
    public T GetValue<T>(string key, T defaultValue);

    /// <summary> 设置值 </summary>
    public void SetValue(string key, string value);

    /// <summary> 按分组获取所有设置 </summary>
    public List<SystemSettingsEntity> GetByGroup(string group);

    /// <summary> 获取所有设置 </summary>
    public List<SystemSettingsEntity> GetAll();

    /// <summary> 批量保存设置（前端提交用）</summary>
    public void SaveBatch(IEnumerable<SystemSettingsEntity> entities);

    /// <summary> 首次启动时从 appsettings.json 迁移种子数据 </summary>
    public void EnsureSeedData(AppSettings fallbackSettings);
}
```

### 种子数据迁移策略

- 系统首次启动时（数据库 `SystemSettings` 集合为空），调用 `EnsureSeedData()` 从嵌入的 `appsettings.json` 读取默认值写入数据库
- 后续启动时跳过迁移，始终从数据库读取
- `AppSettings` 类保留为 **回退默认值**，不再作为运行时主配置源

---

## 四、CortanaDbContext 变更

在 `CortanaDbContext` 中添加新集合和索引：

```csharp
// 新增集合
public ILiteCollection<SystemSettingsEntity> SystemSettings
    => _db.GetCollection<SystemSettingsEntity>("SystemSettings");

// EnsureIndexes() 中新增
SystemSettings.EnsureIndex(x => x.Group);
SystemSettings.EnsureIndex(x => x.SortOrder);
```

---

## 五、启动流程改造

### 5.1 Program.cs 变更

```
[现有] appsettings.json → AppSettings 单例 → 各 Service 构造注入
[改造后]
  1. AppSettings 仍从嵌入资源加载（作为 fallback）
  2. CortanaDbContext 创建后立即调用 EnsureSeedData(appSettings)
  3. 注册 SystemSettingsService 到 DI
  4. 各 Service 改为从 SystemSettingsService 读取配置
```

### 5.2 App.Startup.cs / App.cs 变更

```
[现有] WorkspaceDirectory = UserDataDirectory（固定）
[改造后]
  1. OnApplicationStartup 中通过 SystemSettingsService 读取 "System.WorkspaceDirectory"
  2. 若数据库有值 → 使用数据库值
  3. 若数据库无值 → 使用 UserDataDirectory 默认值
  4. 调用 ChangeWorkspaceDirectory(path) 完成初始化
```

启动时序图：

```
Program.Main()
  ├─ LoadEmbeddedJson<AppSettings>()    // 嵌入资源，作为 fallback
  ├─ ConfigureServices()
  │    ├─ AddSingleton<CortanaDbContext>()
  │    ├─ AddTransient<SystemSettingsService>()   // 新增
  │    └─ ... 其他服务 ...
  ├─ EnsureSeedData(appSettings)        // 首次启动写入种子数据
  └─ WinFormedgeApp.Run()
       └─ App.OnApplicationStartup()
            ├─ SystemSettingsService.GetValue("System.WorkspaceDirectory")
            └─ App.ChangeWorkspaceDirectory(path)
```

---

## 六、各服务改造详情

### 6.1 ChatHistoryDataProvider

**现状：** `MaxContentLength=7500`、`MaxContentCount=15` 硬编码属性

**改造：**
- 构造函数注入 `SystemSettingsService`
- `ProvideChatHistoryAsync` 中动态读取：
  - `ChatHistory.MaxContentLength` → MaxContentLength
  - `ChatHistory.MaxContentCount` → MaxContentCount

### 6.2 WakeWordService

**现状：** 构造注入 `AppSettings`，使用 `appSettings.SherpaOnnx.*`

**改造：**
- 新增构造参数 `SystemSettingsService`
- `ListenLoop()` 中改为从数据库读取 KWS 参数
- `AppSettings` 保留作为回退默认值

### 6.3 SpeechRecognitionService

**现状：** 构造注入 `AppSettings`，使用 `appSettings.SherpaOnnx.Rule*` 和 `RecognitionTimeoutSeconds`

**改造：**
- 新增构造参数 `SystemSettingsService`
- `EnsureModelLoaded()` 和 `RecognitionLoop()` 中从数据库读取

### 6.4 TextToSpeechService

**现状：** 构造注入 `AppSettings`，使用 `appSettings.Tts.Speed`

**改造：**
- 新增构造参数 `SystemSettingsService`
- `SynthesizeLoopAsync()` 中每次合成时读取最新 Speed 值，支持实时调整

### 6.5 改造原则

- **不删除 `AppSettings` 类**，保留作为嵌入资源的默认值回退
- 所有服务优先从 `SystemSettingsService` 读取，读不到时用 `AppSettings` 默认值
- 语音引擎参数（KWS/STT）因为在引擎初始化时一次性设置，修改后需要重启生效

---

## 七、界面拆分方案

### 7.1 现有问题

`settings.html` 单文件包含 6 个选项卡的全部 HTML（约 300+ 行），`settings.js` 也是单文件包含所有逻辑。新增"系统设置"选项卡后将更加臃肿。

### 7.2 拆分策略

将每个选项卡的 HTML 片段拆分为独立文件，通过 `<iframe>` 或 JS 动态加载。推荐 **JS 动态加载方案**：

```
wwwroot/
├── settings.html              # 主框架：侧边栏导航 + 内容容器
├── styles/
│   └── settings.css           # 保持不变
├── js/
│   ├── settings.js            # 主框架逻辑：选项卡切换 + 动态加载
│   ├── settings-providers.js  # AI 厂商管理
│   ├── settings-models.js     # 模型管理
│   ├── settings-agents.js     # 智能体管理
│   ├── settings-mcp.js        # MCP 服务管理
│   ├── settings-tools.js      # 工具管理
│   ├── settings-tuning.js     # 参数微调
│   └── settings-system.js     # 系统设置（新增）
├── partials/
│   ├── settings-providers.html
│   ├── settings-models.html
│   ├── settings-agents.html
│   ├── settings-mcp.html
│   ├── settings-tools.html
│   ├── settings-tuning.html
│   └── settings-system.html   # 新增
```

### 7.3 主框架 settings.html 简化后

```html
<div class="settings-layout">
  <nav class="tab-nav">
    <button class="tab-btn active" data-tab="system">⚙ 系统设置</button>
    <button class="tab-btn" data-tab="providers">🏢 AI 厂商</button>
    <button class="tab-btn" data-tab="models">🔮 模型管理</button>
    <button class="tab-btn" data-tab="agents">🧠 智能体</button>
    <button class="tab-btn" data-tab="mcp">🌐 MCP 服务</button>
    <button class="tab-btn" data-tab="tools">🔧 工具管理</button>
    <button class="tab-btn" data-tab="tuning">⚡ 参数微调</button>
  </nav>
  <div class="tab-content" id="tabContent">
    <!-- 由 JS 按需加载 partials/*.html 片段 -->
  </div>
</div>
```

### 7.4 动态加载机制

```javascript
// settings.js 核心逻辑
async function loadTabContent(tabName) {
    const container = document.getElementById('tabContent');
    const resp = await fetch(`partials/settings-${tabName}.html`);
    container.innerHTML = await resp.text();

    // 动态加载对应 JS 模块
    const script = document.createElement('script');
    script.src = `js/settings-${tabName}.js`;
    document.body.appendChild(script);
}
```

### 7.5 新增系统设置选项卡 (settings-system.html)

```
┌─────────────────────────────────────────────┐
│  ⚙ 系统设置                                 │
├─────────────────────────────────────────────┤
│                                             │
│  📁 系统                                    │
│  ┌─────────────────────────────────────┐    │
│  │ 当前工作目录  [/path/to/workspace][📂] │    │
│  └─────────────────────────────────────┘    │
│                                             │
│  🎙 语音唤醒 (KWS)                          │
│  ┌─────────────────────────────────────┐    │
│  │ 唤醒词灵敏度   ──●──── 0.01        │    │
│  │ 增强分数       ──────●── 9.0        │    │
│  │ 尾部空白帧数   [1]                  │    │
│  └─────────────────────────────────────┘    │
│                                             │
│  🗣 语音识别 (STT)                           │
│  ┌─────────────────────────────────────┐    │
│  │ 无语音超时(秒)  ──────●── 5.0       │    │
│  │ 停顿超时(秒)    ──●──── 2.0         │    │
│  │ 最大时长(秒)    ──────────●── 30.0   │    │
│  │ 空闲超时(秒)    ──────●── 5.0        │    │
│  └─────────────────────────────────────┘    │
│                                             │
│  🔊 语音合成 (TTS)                           │
│  ┌─────────────────────────────────────┐    │
│  │ 语速倍率       ──●──── 1.0          │    │
│  └─────────────────────────────────────┘    │
│                                             │
│  💬 对话历史                                 │
│  ┌─────────────────────────────────────┐    │
│  │ 最大上下文字符数 [7500]              │    │
│  │ 最大保留消息数   [15]                │    │
│  └─────────────────────────────────────┘    │
│                                             │
│          [恢复默认]        [保存设置]        │
└─────────────────────────────────────────────┘
```

---

## 八、SettingsBridgeHostObject 扩展

在现有 `SettingsBridgeHostObject` 中新增系统设置相关方法：

```csharp
// ──────── 系统设置 ────────

/// <summary>
/// 获取所有系统设置（按分组排列），返回 JSON。
/// </summary>
public string GetSystemSettings();

/// <summary>
/// 获取指定分组的系统设置，返回 JSON。
/// </summary>
public string GetSystemSettingsByGroup(string group);

/// <summary>
/// 批量保存系统设置。接收 JSON 数组 [{ Id, Value }, ...]
/// </summary>
public string SaveSystemSettings(string json);

/// <summary>
/// 将所有设置恢复为默认值。
/// </summary>
public string ResetSystemSettings();

/// <summary>
/// 选择工作目录（打开文件夹选择对话框）。
/// </summary>
public string SelectWorkspaceDirectory();
```

---

## 九、实施步骤

按优先级和依赖关系排序：

### 阶段 1：数据层（Netor.Cortana.Entitys 项目）

| 步骤 | 内容 | 文件 |
|------|------|------|
| 1.1 | 创建 `SystemSettingsEntity` 实体类 | `SystemSettingsEntity.cs` |
| 1.2 | `CortanaDbContext` 添加集合和索引 | `CortanaDbContext.cs` |
| 1.3 | 创建 `SystemSettingsService` 服务 | `Services/SystemSettingsService.cs` |

### 阶段 2：启动流程改造（Netor.Cortana 项目）

| 步骤 | 内容 | 文件 |
|------|------|------|
| 2.1 | DI 注册 `SystemSettingsService` | `Program.cs` |
| 2.2 | 启动时调用 `EnsureSeedData()` | `Program.cs` |
| 2.3 | `App.OnApplicationStartup` 从数据库读取 WorkspaceDirectory | `App.cs` |

### 阶段 3：各服务改造

| 步骤 | 内容 | 文件 |
|------|------|------|
| 3.1 | `ChatHistoryDataProvider` 从数据库读取限制值 | `Providers/ChatHistoryDataProvider.cs` |
| 3.2 | `WakeWordService` 从数据库读取 KWS 参数 | `Services/WakeWordService.cs` |
| 3.3 | `SpeechRecognitionService` 从数据库读取 STT 参数 | `Services/SpeechRecognitionService.cs` |
| 3.4 | `TextToSpeechService` 从数据库读取 TTS 参数 | `Services/TextToSpeechService.cs` |

### 阶段 4：界面层

| 步骤 | 内容 | 文件 |
|------|------|------|
| 4.1 | `SettingsBridgeHostObject` 新增系统设置方法 | `Pages/SettingsWindow.BridgeHostObject.cs` |
| 4.2 | 拆分 `settings.html` 为主框架 + partials | `wwwroot/settings.html` + `wwwroot/partials/*.html` |
| 4.3 | 拆分 `settings.js` 为模块化 JS | `wwwroot/js/settings-*.js` |
| 4.4 | 新增系统设置页面 | `wwwroot/partials/settings-system.html` + `wwwroot/js/settings-system.js` |

---

## 十、风险评估与注意事项

### 10.1 兼容性

- `AppSettings` 类和 `appsettings.json` **保留不删除**，作为回退默认值源
- 老版本数据库无 `SystemSettings` 集合时，`EnsureSeedData()` 自动创建并填充
- 新增的 `SystemSettingsService` 所有 `GetValue` 方法均有默认值参数，不会因数据库缺失导致崩溃

### 10.2 语音引擎参数

- KWS（唤醒词检测）和 STT（语音识别）参数在 **引擎初始化时一次性设置**
- 在界面修改这些参数后，需提示用户 **"重启应用后生效"**
- TTS 语速可实时生效（每次合成时读取最新值）

### 10.3 界面拆分

- 动态加载 partials 依赖 WebView2 对 `fetch()` 的支持（WinFormedge 基于 WebView2，完全支持）
- 各 JS 模块需要访问 `bridge` 对象和 `showToast()` 公共方法，这些定义在主 `settings.js` 中，需在模块加载前确保可用
- CSS 保持单文件不拆分，避免加载顺序问题

### 10.4 工作目录持久化

- 用户通过界面选择工作目录后，同时调用 `App.ChangeWorkspaceDirectory()` 和 `SystemSettingsService.SetValue()`
- 下次启动时从数据库读取，无需用户重新选择
- 如果数据库记录的目录不存在（如 U 盘拔出），回退到 `UserDataDirectory`

---

## 十一、文件清单汇总

### 新增文件

| 文件路径 | 说明 |
|---------|------|
| `Netor.Cortana.Entitys/SystemSettingsEntity.cs` | 系统设置实体 |
| `Netor.Cortana.Entitys/Services/SystemSettingsService.cs` | 系统设置服务 |
| `wwwroot/partials/settings-system.html` | 系统设置页面片段 |
| `wwwroot/partials/settings-providers.html` | AI 厂商页面片段 |
| `wwwroot/partials/settings-models.html` | 模型管理页面片段 |
| `wwwroot/partials/settings-agents.html` | 智能体页面片段 |
| `wwwroot/partials/settings-mcp.html` | MCP 服务页面片段 |
| `wwwroot/partials/settings-tools.html` | 工具管理页面片段 |
| `wwwroot/partials/settings-tuning.html` | 参数微调页面片段 |
| `wwwroot/js/settings-system.js` | 系统设置交互逻辑 |
| `wwwroot/js/settings-providers.js` | AI 厂商交互逻辑 |
| `wwwroot/js/settings-models.js` | 模型管理交互逻辑 |
| `wwwroot/js/settings-agents.js` | 智能体交互逻辑 |
| `wwwroot/js/settings-mcp.js` | MCP 服务交互逻辑 |
| `wwwroot/js/settings-tools.js` | 工具管理交互逻辑 |
| `wwwroot/js/settings-tuning.js` | 参数微调交互逻辑 |

### 修改文件

| 文件路径 | 变更内容 |
|---------|---------|
| `Netor.Cortana.Entitys/CortanaDbContext.cs` | 添加 SystemSettings 集合和索引 |
| `Netor.Cortana/Program.cs` | 注册 SystemSettingsService + 种子数据 |
| `Netor.Cortana/App.cs` | 启动时从 DB 读取 WorkspaceDirectory |
| `Netor.Cortana/Providers/ChatHistoryDataProvider.cs` | 从 DB 读取限制值 |
| `Netor.Cortana/Services/WakeWordService.cs` | 从 DB 读取 KWS 参数 |
| `Netor.Cortana/Services/SpeechRecognitionService.cs` | 从 DB 读取 STT 参数 |
| `Netor.Cortana/Services/TextToSpeechService.cs` | 从 DB 读取 TTS 参数 |
| `Netor.Cortana/Pages/SettingsWindow.BridgeHostObject.cs` | 新增系统设置 CRUD 方法 |
| `wwwroot/settings.html` | 简化为主框架 + 动态加载 |
| `wwwroot/js/settings.js` | 简化为选项卡切换 + 模块加载器 |
