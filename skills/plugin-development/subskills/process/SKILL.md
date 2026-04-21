---
name: process
description: 'Cortana Process 插件开发子技能。位置：plugin-development/subskills/process。用于开发以独立 exe 进程运行的插件，支持 JIT self-contained 和 AOT exe 两种发布方式，通过 stdin/stdout JSON 协议与宿主通信。触发关键词：Process 插件、进程插件、exe 插件、JIT 插件、进程隔离插件。'
user-invocable: true
---

# Plugin Development Process

Process 通道启动一个独立的 exe 子进程作为插件。宿主通过 stdin/stdout 单行 JSON 协议通信。
插件无需 AOT 发布，可以使用完整 .NET JIT 生态（反射、Roslyn、动态编译等）。

## Flow

1. 创建普通 .NET Console 项目（OutputType=Exe）。
2. 实现 stdin/stdout JSON 消息循环（或等待官方 SDK 包）。
3. 编写 get_info / init / invoke / destroy 处理逻辑。
4. 撰写 plugin.json（runtime: process，command 指向 exe）。
5. 选择发布方式：JIT self-contained 或 AOT exe。
6. 运行时安装切到 publish-install 子技能。

## Rules

- stdin 每次只读一行 JSON，stdout 每次只写一行 JSON，写完必须 Flush。
- get_info 必须返回完整的插件元数据（id、name、version、tools 列表）。
- init 接收宿主注入的 dataDirectory / workspaceDirectory / wsPort / pluginDirectory。
- invoke 接收 toolName + argsJson，返回结果字符串（可以是 JSON 或纯文本）。
- destroy 收到后执行清理，然后退出进程（Exit(0)）。
- 任何异常必须捕获并以 `{ "success": false, "error": "..." }` 响应，不能让进程崩溃。
- stderr 输出会被宿主收集并写入日志，可用于调试。
- 不在 stdout 输出任何非协议内容（Console.WriteLine 只用于协议响应）。

## Protocol

协议详细定义和 C# 实现模板见：

- resources/csharp-process-plugin.md

## Publish

两种发布方式均支持，无需选边：

| 方式 | 命令 | 特点 |
|------|------|------|
| JIT self-contained | `dotnet publish -r win-x64 --self-contained` | 包含 Runtime，无外部依赖，~60-100 MB |
| AOT exe | `dotnet publish -r win-x64 /p:PublishAot=true` | 启动更快，体积更小，但有 AOT 约束 |

## Resources

- resources/csharp-process-plugin.md
