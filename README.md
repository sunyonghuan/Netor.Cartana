# Netor.Cortana

> 基于 .NET 10 的 Windows 桌面 AI 助手，当前主界面为 AvaloniaUI，集成 LLM、语音、插件系统、MCP 和 WebSocket 接入能力。

## 项目现状

Netor.Cortana 当前已经不是单体桌面程序，而是一个围绕 AI 助手场景拆分出的模块化解决方案。仓库内同时包含：

- 当前主桌面程序 Netor.Cortana.AvaloniaUI
- 遗留桌面程序 Netor.Cortana
- AI、语音、网络、插件等独立业务模块
- Native 插件宿主、插件抽象层和 Native 插件开发包
- 示例插件、发布脚本和项目文档

当前能力覆盖：

- 多模型对话与 Agent 工具调用
- 语音唤醒、语音识别、语音合成
- WebSocket 对外接入
- Native、MCP 为主的扩展通道，Dotnet 仅保留兼容能力
- 桌面控制、文件操作、PowerShell 集成
- SQLite 持久化和 Serilog 日志

## 核心说明

### UI 形态

- Netor.Cortana.AvaloniaUI：当前主项目，已经承接新的桌面界面和后续功能迭代，Release 配置使用 Native AOT 发布
- Netor.Cortana：旧 WinForms 宿主，保留在仓库中用于历史兼容和参考，不再作为推荐运行入口

README 以当前仓库整体为准，但默认入口、运行说明和发布说明均以 AvaloniaUI 为主。

### 扩展通道

| 通道 | 当前状态 | 说明 |
|------|----------|------|
| Native | 推荐 | 独立 NativeHost 子进程承载，适合 AOT、高隔离和高性能场景 |
| MCP | 推荐 | 通过 stdio、sse、streamable-http 连接外部工具服务 |
| Dotnet | 遗留兼容 | 旧托管插件体系，仅用于历史插件兼容和迁移，不建议新开发继续使用 |

### 技术栈

| 组件 | 技术 |
|------|------|
| 运行时 | .NET 10 |
| 主 UI | Avalonia 12 |
| 遗留 UI | WinForms + WinFormedge |
| AI 编排 | Microsoft.Extensions.AI、Microsoft.Agents |
| MCP | ModelContextProtocol 1.2.0 |
| 语音 | Sherpa-ONNX |
| 数据存储 | SQLite |
| 日志 | Serilog |
| 插件主路线 | Native 模式 + MCP 模式 |

## 快速开始

### 环境要求

- Windows 10/11 x64
- .NET 10 SDK
- PowerShell 7

### 构建

```powershell
dotnet build .\Netor.Cortana.slnx
```

### 运行

运行当前主项目：

```powershell
dotnet run --project .\Src\Netor.Cortana.AvaloniaUI\Netor.Cortana.AvaloniaUI.csproj
```

运行旧界面版本，仅用于兼容验证或历史参考：

```powershell
dotnet run --project .\Src\Netor.Cortana\Netor.Cortana.csproj
```

### 发布

仓库当前使用多个专用发布脚本，输出目录统一位于 Realases。

```powershell
# 发布当前主项目 AvaloniaUI
.\avaloniaui.publish.ps1

# 发布旧 WinForms 项目链路
.\cortana.publish.ps1

# 一键发布旧主程序链路 + NativeHost + NativeTestPlugin
.\publish.ps1

# 打包并推送插件开发相关 NuGet 包
.\plugin.publish.ps1
```

常见输出目录：

- Realases/Cortana
- Realases/AvaloniaUI
- Realases/Nupkgs

## 项目目录结构

```
Netor.Cortana/                          # Git 仓库根目录
└── Src/Netor.Cortana/                  # 解决方案根目录
    ├── Netor.Cortana.slnx              # 解决方案文件
    ├── publish.ps1                     # 旧主程序链路一键发布
    ├── cortana.publish.ps1             # 旧 WinForms 项目发布
    ├── avaloniaui.publish.ps1          # 当前主项目 AvaloniaUI 发布
    ├── plugin.publish.ps1              # 插件开发包 NuGet 发布
    │
    ├── Src/                            # 源代码
    │   ├── Netor.Cortana.AvaloniaUI/   # 🏠 当前主项目 UI（Avalonia 12，Release 走 AOT）
    │   ├── Netor.Cortana/              #    遗留 UI 项目（WinForms + WinFormedge）
    │   ├── Netor.Cortana.AI/           # 🤖 AI 编排、模型接入、Agent 能力
    │   ├── Netor.Cortana.Voice/        # 🔊 语音能力（KWS/STT/TTS）
    │   ├── Netor.Cortana.Networks/     # 🌐 网络接口与 WebSocket 服务
    │   ├── Netor.Cortana.Plugin/       # 🔌 插件加载、通道路由、运行时管理
    │   ├── Netor.Cortana.Entitys/      # 📦 数据实体、SQLite 与配置持久化
    │   ├── KokoroAudition/             # 🎵 TTS 相关实验工程
    │   └── Plugins/                    # 🧩 插件基础设施与开发包
    │       ├── Netor.Cortana.NativeHost/           # Native 插件宿主子进程
    │       ├── Netor.Cortana.Plugin.Abstractions/  # 插件契约层
    │       ├── Netor.Cortana.Plugin.Native/        # Native 插件开发包
    │       └── Netor.Cortana.Plugin.Native.Generator/ # Native 插件源码生成器
    │
    ├── Samples/                        # 📝 示例插件
    │   ├── SamplePlugins/              #    Dotnet 示例插件
    │   ├── NativeTestPlugin/           #    Native AOT 示例插件
    │   ├── ReminderPlugin/             #    提醒事项插件样例
    │   └── ReminderPlugin.Tests/       #    提醒插件测试项目
    │
    ├── Realases/                       # 📦 发布输出
    ├── Docs/                           # 📚 项目文档
    ├── Res/                            # 🎨 资源文件（图标等）
    └── .github/                        # GitHub/CI 配置
```

## 项目定位

- Netor.Cortana.AvaloniaUI 是当前默认开发、调试、发布和验收入口。
- Netor.Cortana 保留在仓库中，但界面、性能和功能已经落后，不再作为主产品线维护。
- 插件体系当前以 Native 和 MCP 为主；Dotnet 插件契约属于历史方案，不再作为默认扩展模式。
- 如果文档、脚本或说明与两套 UI 的事实不一致，以 AvaloniaUI 为准，并优先修正文档。

## 插件系统

Cortana 当前推荐两条主扩展路线：Native 和 MCP。Dotnet 通道仍存在于仓库和运行时中，但属于兼容保留能力，不再是推荐的插件开发方向。

| 通道 | 运行方式 | 适用场景 | 隔离级别 |
|------|---------|---------|---------|
| Native | NativeHost 子进程 + NativeLibrary | C/C++/Rust/C# AOT、高性能计算、高隔离 | 进程级（崩溃隔离） |
| MCP | Model Context Protocol 客户端 | 远程服务集成、跨语言工具 | 进程级/网络级 |
| Dotnet | AssemblyLoadContext 加载 | 历史托管插件兼容、迁移过渡 | 进程内（ALC 隔离） |

### 本地插件目录结构

本地插件部署在 .cortana/plugins 目录下。当前建议的新本地插件以 Native 模式为主；plugin.json 仍可用于 Dotnet 和 Native 两类本地插件。MCP 通道通过 UI 和数据库配置，不使用 plugin.json 部署。

```
.cortana/plugins/
└── my-native-plugin/
    ├── plugin.json          # 插件清单（必需）
    └── MyNativeLib.dll      # AOT 原生 DLL
```

### plugin.json 清单文件

```json
{
  "id": "com.example.my-plugin",
  "name": "我的插件",
  "version": "1.0.0",
  "description": "插件描述",
  "runtime": "native",
  "libraryName": "MyNativeLib.dll",
  "minHostVersion": "1.0.0"
}
```

- 新插件默认按 Native 字段组织。
- 历史 Dotnet 插件仍然使用 assemblyName，但不再作为本文档默认模板。
- MCP 通道不通过 plugin.json 注册，而是通过设置界面或数据库记录配置连接信息。

> 详细的插件开发指南请参阅：
> - [Docs/plugin-native.md](Docs/plugin-native.md) — Native 原生插件开发
> - [Docs/plugin-mcp.md](Docs/plugin-mcp.md) — MCP 服务器集成
> - [Docs/plugin-dotnet.md](Docs/plugin-dotnet.md) — Dotnet 托管插件开发（历史兼容）

## 文档索引

| 文档 | 说明 |
|------|------|
| [Docs/plugin-native.md](Docs/plugin-native.md) | Native 原生插件开发指南 |
| [Docs/plugin-mcp.md](Docs/plugin-mcp.md) | MCP 服务器集成指南 |
| [Docs/plugin-dotnet.md](Docs/plugin-dotnet.md) | Dotnet 托管插件开发指南（历史兼容） |
| [Docs/websocket-api.md](Docs/websocket-api.md) | WebSocket 接入协议与消息格式 |
| [Docs/class-reference.md](Docs/class-reference.md) | 核心类文件说明 |

## 许可证

本项目为内部项目，未经授权不得分发。


