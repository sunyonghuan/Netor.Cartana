# Netor.Cortana 插件开发者指南

> 版本：1.0.0 | 日期：2025-07

> 历史说明：本文档形成时仍把 Dotnet 托管插件视为推荐路线。当前项目已经切换为 Native 和 MCP 优先，阅读本文件时请将其中的 Dotnet 内容视为兼容保留方案。

---

## 一、概述

Netor.Cortana 的扩展能力当前以 Native 和 MCP 为主；下表保留的是这份旧文档形成时覆盖的三类方式，其中 Dotnet 仅适合历史兼容：

| 方式 | 适用语言 | 开发门槛 | 说明 |
|------|---------|---------|------|
| 原生 DLL 插件 | C# AOT、Rust、C++、Go | 中等 | 当前推荐的本地插件主路线 |
| MCP / 外部服务接入 | Python、Node.js、Java、任意 | 低 | 当前推荐的跨语言/远程工具路线 |
| .NET 托管插件 | C#、F#、VB | 最低 | 历史兼容方案，直接实现 `IPlugin` 接口 |

---

## 二、.NET 托管插件（历史兼容）

### 2.1 创建项目

```bash
dotnet new classlib -n MyPlugin -f net10.0
cd MyPlugin
dotnet add package Netor.Cortana.Plugin.Abstractions
```

### 2.2 实现 IPlugin

```csharp
using Microsoft.Extensions.AI;
using Netor.Cortana.Plugin.Abstractions;

public class WeatherPlugin : IPlugin, IDisposable
{
    private HttpClient? _http;
    private readonly List<AITool> _tools = [];

    // ── 元信息 ──
    public string Id => "com.example.weather";
    public string Name => "天气查询";
    public Version Version => new(1, 0, 0);
    public string Description => "为 AI 添加天气查询能力";
    public IReadOnlyList<string> Tags => ["weather", "utility"];
    public IReadOnlyList<AITool> Tools => _tools;

    public string? Instructions => """
        当用户询问天气时，调用 get_weather 工具，传入城市名称。
        支持中国所有城市。
        """;

    // ── 初始化 ──
    public Task InitializeAsync(IPluginContext context)
    {
        _http = context.HttpClientFactory.CreateClient();

        _tools.Add(AIFunctionFactory.Create(
            name: "get_weather",
            description: "查询指定城市的当前天气",
            method: (string city) => GetWeatherAsync(city)));

        _tools.Add(AIFunctionFactory.Create(
            name: "get_forecast",
            description: "查询指定城市未来 3 天天气预报",
            method: (string city, int days) => GetForecastAsync(city, days)));

        return Task.CompletedTask;
    }

    // ── 工具实现 ──
    private async Task<string> GetWeatherAsync(string city)
    {
        // 调用天气 API ...
        return $"{city}：晴，25°C，湿度 45%";
    }

    private async Task<string> GetForecastAsync(string city, int days)
    {
        return $"{city}未来{days}天：晴→多云→小雨";
    }

    public void Dispose() => _http?.Dispose();
}
```

### 2.3 一个 DLL 中包含多个插件

一个项目可以包含多个 `IPlugin` 实现，宿主会**全部扫描、全部加载**：

```csharp
// 同一个 DLL 中
public class CSharpPlugin : IPlugin { /* 12 个工具 */ }
public class PythonPlugin : IPlugin { /* 10 个工具 */ }
public class JavaPlugin : IPlugin   { /* 6 个工具 */ }
// 宿主加载后得到 3 个插件实例，28 个工具
```

### 2.4 创建 plugin.json

```json
{
  "id": "com.example.weather",
  "name": "天气查询",
  "version": "1.0.0",
  "description": "为 AI 添加天气查询能力",
  "runtime": "dotnet",
  "assemblyName": "MyPlugin.dll",
  "targetFramework": "net10.0",
  "abstractionsVersion": "1.0.0",
  "minHostVersion": "1.0.0"
}
```

### 2.5 发布与安装

```bash
dotnet publish -c Release -o ./publish
```

将 `publish/` 目录下的所有文件复制到：

```
{宿主用户数据目录}/plugins/my-weather-plugin/
├── plugin.json
├── MyPlugin.dll
├── MyPlugin.deps.json
└── (其他依赖 DLL)
```

### 2.6 发布方式限制

| 发布方式 | 是否允许 |
|---------|---------|
| `dotnet build` / `dotnet publish` | ✅ |
| `PublishReadyToRun=true` | ✅ |
| `PublishAot=true` | ❌ 请使用原生 DLL 模式 |
| `PublishSingleFile=true` | ❌ |
| `PublishTrimmed=true` | ⚠️ 需配置 `<TrimmerRootAssembly>` 保留插件类型 |

---

## 三、原生 DLL 插件（C# AOT / Rust / C++ / Go）

### 3.1 标准导出函数

无论使用什么语言，原生 DLL 必须导出以下 C 函数：

| 函数名 | 签名 | 用途 |
|--------|------|------|
| `cortana_plugin_get_info` | `() → char*` | 返回插件信息 JSON（UTF-8） |
| `cortana_plugin_init` | `(char* configJson) → int` | 初始化，0=成功 |
| `cortana_plugin_invoke` | `(char* toolName, char* argsJson) → char*` | 调用工具，返回结果 JSON |
| `cortana_plugin_free` | `(char* ptr) → void` | 释放由插件分配的内存 |
| `cortana_plugin_destroy` | `() → void` | 销毁插件，释放所有资源 |

`cortana_plugin_get_info` 返回的 JSON 格式：

```json
{
  "id": "com.example.weather",
  "name": "天气查询",
  "version": "1.0.0",
  "tools": [
    {
      "name": "get_weather",
      "description": "查询指定城市的当前天气",
      "parameters": [
        { "name": "city", "type": "string", "description": "城市名称", "required": true }
      ]
    }
  ],
  "instructions": "当用户询问天气时，调用 get_weather 工具。"
}
```

`cortana_plugin_invoke` 的参数和返回值均为 UTF-8 JSON 字符串。

### 3.2 C# AOT 示例

```csharp
using System.Runtime.InteropServices;

public static class PluginExports
{
    [UnmanagedCallersOnly(EntryPoint = "cortana_plugin_get_info")]
    public static IntPtr GetInfo()
    {
        var json = """
        {
          "id": "com.example.weather",
          "name": "天气查询 (AOT)",
          "version": "1.0.0",
          "tools": [
            {"name":"get_weather","description":"查询城市天气",
             "parameters":[{"name":"city","type":"string","required":true}]}
          ]
        }
        """;
        return Marshal.StringToCoTaskMemUTF8(json);
    }

    [UnmanagedCallersOnly(EntryPoint = "cortana_plugin_init")]
    public static int Init(IntPtr configJsonPtr) => 0;

    [UnmanagedCallersOnly(EntryPoint = "cortana_plugin_invoke")]
    public static IntPtr Invoke(IntPtr toolNamePtr, IntPtr argsJsonPtr)
    {
        var toolName = Marshal.PtrToStringUTF8(toolNamePtr)!;
        var argsJson = Marshal.PtrToStringUTF8(argsJsonPtr)!;

        var result = toolName switch
        {
            "get_weather" => """{"content":"北京：晴，25°C"}""",
            _ => """{"error":"未知工具"}"""
        };
        return Marshal.StringToCoTaskMemUTF8(result);
    }

    [UnmanagedCallersOnly(EntryPoint = "cortana_plugin_free")]
    public static void Free(IntPtr ptr) => Marshal.FreeCoTaskMem(ptr);

    [UnmanagedCallersOnly(EntryPoint = "cortana_plugin_destroy")]
    public static void Destroy() { }
}
```

项目配置：

```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <OutputType>Library</OutputType>
</PropertyGroup>
```

发布：`dotnet publish -c Release`

### 3.3 Rust 示例

```rust
use std::ffi::{CStr, CString};
use std::os::raw::c_char;

#[no_mangle]
pub extern "C" fn cortana_plugin_get_info() -> *mut c_char {
    let json = r#"{
        "id": "com.example.sysinfo",
        "name": "系统信息",
        "version": "1.0.0",
        "tools": [
            {"name":"get_cpu_usage","description":"获取 CPU 使用率","parameters":[]}
        ]
    }"#;
    CString::new(json).unwrap().into_raw()
}

#[no_mangle]
pub extern "C" fn cortana_plugin_init(_config: *const c_char) -> i32 { 0 }

#[no_mangle]
pub extern "C" fn cortana_plugin_invoke(
    tool_name: *const c_char,
    _args_json: *const c_char,
) -> *mut c_char {
    let name = unsafe { CStr::from_ptr(tool_name) }.to_str().unwrap();
    let result = match name {
        "get_cpu_usage" => r#"{"content":"CPU 使用率: 23%"}"#,
        _ => r#"{"error":"未知工具"}"#,
    };
    CString::new(result).unwrap().into_raw()
}

#[no_mangle]
pub extern "C" fn cortana_plugin_free(ptr: *mut c_char) {
    if !ptr.is_null() { unsafe { drop(CString::from_raw(ptr)); } }
}

#[no_mangle]
pub extern "C" fn cortana_plugin_destroy() {}
```

Cargo.toml:

```toml
[lib]
crate-type = ["cdylib"]
```

### 3.4 plugin.json

```json
{
  "id": "com.example.sysinfo",
  "name": "系统信息",
  "version": "1.0.0",
  "runtime": "native",
  "libraryName": "sysinfo_plugin.dll",
  "abstractionsVersion": "1.0.0",
  "minHostVersion": "1.0.0"
}
```

---

## 四、子进程插件（Python / Node.js / 任意语言）

### 4.1 通信协议

宿主通过 stdin/stdout 与插件进程以 JSON-RPC 2.0 格式通信，每行一个 JSON 对象。

**请求方法：**

| method | 用途 | params |
|--------|------|--------|
| `getInfo` | 获取插件信息 | 无 |
| `invoke` | 调用工具 | `{ "tool": "...", "args": {...} }` |
| `shutdown` | 通知插件退出 | 无 |

### 4.2 Python 示例

```python
#!/usr/bin/env python3
import json
import sys
import urllib.request

def get_weather(city: str) -> str:
    # 实际调用天气 API...
    return f"{city}：晴，25°C，湿度 45%"

def handle(req: dict) -> dict:
    method = req["method"]

    if method == "getInfo":
        return {
            "id": "com.example.weather-py",
            "name": "天气查询 (Python)",
            "version": "1.0.0",
            "tools": [
                {
                    "name": "get_weather",
                    "description": "查询指定城市的当前天气",
                    "parameters": [
                        {"name": "city", "type": "string", "required": True}
                    ]
                }
            ],
            "instructions": "用户问天气时调用 get_weather。"
        }

    elif method == "invoke":
        tool = req["params"]["tool"]
        args = req["params"]["args"]
        if tool == "get_weather":
            return {"content": get_weather(args["city"])}
        return {"error": f"未知工具: {tool}"}

    elif method == "shutdown":
        sys.exit(0)

# 主循环
for line in sys.stdin:
    line = line.strip()
    if not line:
        continue
    req = json.loads(line)
    result = handle(req)
    response = {"jsonrpc": "2.0", "id": req.get("id"), "result": result}
    print(json.dumps(response, ensure_ascii=False), flush=True)
```

### 4.3 Node.js 示例

```javascript
const readline = require('readline');
const rl = readline.createInterface({ input: process.stdin });

function handle(req) {
    if (req.method === 'getInfo') {
        return {
            id: 'com.example.translate-node',
            name: '翻译工具 (Node.js)',
            version: '1.0.0',
            tools: [
                { name: 'translate', description: '翻译文本',
                  parameters: [
                    { name: 'text', type: 'string', required: true },
                    { name: 'targetLang', type: 'string', required: true }
                  ]}
            ]
        };
    }

    if (req.method === 'invoke') {
        const { tool, args } = req.params;
        if (tool === 'translate') {
            return { content: `[翻译] ${args.text} → (${args.targetLang}) ...` };
        }
        return { error: `未知工具: ${tool}` };
    }

    if (req.method === 'shutdown') {
        process.exit(0);
    }
}

rl.on('line', (line) => {
    const req = JSON.parse(line);
    const result = handle(req);
    console.log(JSON.stringify({ jsonrpc: '2.0', id: req.id, result }));
});
```

### 4.4 plugin.json

```json
{
  "id": "com.example.weather-py",
  "name": "天气查询 (Python)",
  "version": "1.0.0",
  "runtime": "process",
  "command": "python main.py",
  "abstractionsVersion": "1.0.0",
  "minHostVersion": "1.0.0"
}
```

> `command` 中的相对路径基于插件目录。宿主启动进程时将工作目录设为插件目录。

---

## 五、安装与调试

### 5.1 安装位置

将插件目录复制到宿主的 plugins 目录：

```
{UserDataDirectory}/plugins/{你的插件名}/
├── plugin.json         ← 必须
├── (DLL / 脚本文件)    ← 根据 runtime 类型
└── (依赖文件)
```

### 5.2 调试建议

- **dotnet 插件**：在 Visual Studio 中附加到宿主进程调试
- **native 插件**：使用 WinDbg 或 Visual Studio 原生调试器
- **process 插件**：可直接从命令行运行脚本，手动输入 JSON 测试
