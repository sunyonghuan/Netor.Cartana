# C# Process 插件开发指南

> **注意**：后续将提供官方 SDK 包（`Netor.Cortana.Plugin.Process`），封装消息循环，
> 届时只需关注业务逻辑。当前版本需手动实现协议层（直接复制本文模板即可）。

---

## 一、项目结构

```text
MyPlugin/
├── MyPlugin.csproj
├── Program.cs          ← 消息循环（协议层，复制模板后不需修改）
├── PluginInfo.cs       ← 插件元数据定义
├── MessageHandler.cs   ← init / invoke / destroy 业务入口
├── Protocol/
│   ├── HostRequest.cs  ← 协议请求结构（复制模板）
│   └── HostResponse.cs ← 协议响应结构（复制模板）
└── Tools/
    └── MyTools.cs      ← 工具实现
```

发布产物（plugin 目录）：

```text
my-plugin/
├── plugin.json
└── MyPlugin.exe        ← JIT self-contained 或 AOT exe
```

---

## 二、csproj 配置

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- JIT 发布时不需要 PublishAot，AOT 发布时取消注释 -->
    <!-- <PublishAot>true</PublishAot> -->
  </PropertyGroup>
</Project>
```

> AOT 发布时需遵循 AOT 约束（禁止动态反射、Emit 等），参考 native 子技能的 AOT Rules。

---

## 三、协议结构（复制到 Protocol/ 目录，不需要 NuGet 依赖）

### Protocol/HostRequest.cs

```csharp
using System.Text.Json.Serialization;

namespace MyPlugin.Protocol;

internal sealed record HostRequest
{
    [JsonPropertyName("method")]   public string Method   { get; init; } = string.Empty;
    [JsonPropertyName("toolName")] public string? ToolName { get; init; }
    [JsonPropertyName("args")]     public string? Args     { get; init; }
}
```

### Protocol/HostResponse.cs

```csharp
using System.Text.Json.Serialization;

namespace MyPlugin.Protocol;

internal sealed record HostResponse
{
    [JsonPropertyName("success")] public bool    Success { get; init; }
    [JsonPropertyName("data")]    public string? Data    { get; init; }
    [JsonPropertyName("error")]   public string? Error   { get; init; }

    internal static HostResponse Ok(string? data)   => new() { Success = true,  Data  = data  };
    internal static HostResponse Fail(string error) => new() { Success = false, Error = error };
}
```

### Protocol/PluginJsonContext.cs（AOT 必须，JIT 可选但推荐）

```csharp
using System.Text.Json.Serialization;

namespace MyPlugin.Protocol;

[JsonSerializable(typeof(HostRequest))]
[JsonSerializable(typeof(HostResponse))]
[JsonSerializable(typeof(PluginInfoData))]
[JsonSerializable(typeof(InitConfig))]
internal sealed partial class PluginJsonContext : JsonSerializerContext;
```

---

## 四、插件元数据结构

### PluginInfo.cs

```csharp
using System.Text.Json.Serialization;

namespace MyPlugin;

/// <summary>get_info 响应的 data 字段内容（序列化为 JSON 字符串）。</summary>
internal sealed record PluginInfoData
{
    [JsonPropertyName("id")]           public string           Id           { get; init; } = string.Empty;
    [JsonPropertyName("name")]         public string           Name         { get; init; } = string.Empty;
    [JsonPropertyName("version")]      public string           Version      { get; init; } = string.Empty;
    [JsonPropertyName("description")]  public string           Description  { get; init; } = string.Empty;
    [JsonPropertyName("instructions")] public string?          Instructions { get; init; }
    [JsonPropertyName("tags")]         public List<string>?    Tags         { get; init; }
    [JsonPropertyName("tools")]        public List<ToolInfoData> Tools      { get; init; } = [];
}

internal sealed record ToolInfoData
{
    [JsonPropertyName("name")]        public string                   Name        { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string                   Description { get; init; } = string.Empty;
    [JsonPropertyName("parameters")]  public List<ParameterInfoData>? Parameters  { get; init; }
}

internal sealed record ParameterInfoData
{
    [JsonPropertyName("name")]        public string Name        { get; init; } = string.Empty;
    [JsonPropertyName("type")]        public string Type        { get; init; } = "string";
    [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;
    [JsonPropertyName("required")]    public bool   Required    { get; init; }
}

/// <summary>init 方法收到的 args 字段内容。</summary>
internal sealed record InitConfig
{
    [JsonPropertyName("dataDirectory")]      public string DataDirectory      { get; init; } = string.Empty;
    [JsonPropertyName("workspaceDirectory")] public string WorkspaceDirectory { get; init; } = string.Empty;
    [JsonPropertyName("wsPort")]             public int    WsPort             { get; init; }
    [JsonPropertyName("pluginDirectory")]    public string PluginDirectory    { get; init; } = string.Empty;
}
```

---

## 五、消息循环（Program.cs 完整模板）

```csharp
using System.Text.Json;
using MyPlugin.Protocol;

// stdout 不缓冲，确保每行写出后立即发送
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding  = System.Text.Encoding.UTF8;

var handler = new MessageHandler();
var opts    = new JsonSerializerOptions { TypeInfoResolver = PluginJsonContext.Default };

while (Console.ReadLine() is string line)
{
    HostRequest? req;
    try
    {
        req = JsonSerializer.Deserialize(line, PluginJsonContext.Default.HostRequest);
        if (req is null) continue;
    }
    catch (Exception ex)
    {
        WriteResponse(HostResponse.Fail($"parse error: {ex.Message}"));
        continue;
    }

    HostResponse resp;
    try
    {
        resp = req.Method switch
        {
            "get_info" => HandleGetInfo(),
            "init"     => handler.Init(req.Args),
            "invoke"   => handler.Invoke(req.ToolName, req.Args),
            "destroy"  => handler.Destroy(),
            _          => HostResponse.Fail($"unknown method: {req.Method}")
        };
    }
    catch (Exception ex)
    {
        resp = HostResponse.Fail(ex.Message);
    }

    WriteResponse(resp);

    if (req.Method == "destroy")
        break;
}

Environment.Exit(0);

static void WriteResponse(HostResponse resp)
{
    Console.WriteLine(JsonSerializer.Serialize(resp, PluginJsonContext.Default.HostResponse));
}

static HostResponse HandleGetInfo()
{
    var info = new PluginInfoData
    {
        Id          = "my-plugin",
        Name        = "我的插件",
        Version     = "1.0.0",
        Description = "插件功能描述",
        Tools       =
        [
            new ToolInfoData
            {
                Name        = "my_tool",
                Description = "工具描述",
                Parameters  =
                [
                    new ParameterInfoData { Name = "input", Type = "string", Description = "输入内容", Required = true }
                ]
            }
        ]
    };
    // data 字段必须是已序列化的 JSON 字符串
    var dataJson = JsonSerializer.Serialize(info, PluginJsonContext.Default.PluginInfoData);
    return HostResponse.Ok(dataJson);
}
```

---

## 六、MessageHandler.cs 模板

```csharp
using System.Text.Json;
using MyPlugin.Protocol;

namespace MyPlugin;

internal sealed class MessageHandler
{
    private InitConfig _config = new();

    public HostResponse Init(string? argsJson)
    {
        if (string.IsNullOrEmpty(argsJson))
            return HostResponse.Fail("missing args");

        _config = JsonSerializer.Deserialize(argsJson, PluginJsonContext.Default.InitConfig)
                  ?? throw new InvalidOperationException("init args 反序列化失败");

        // TODO: 在此初始化业务资源（数据库、缓存等）
        return HostResponse.Ok(null);
    }

    public HostResponse Invoke(string? toolName, string? argsJson)
    {
        return toolName switch
        {
            "my_tool" => InvokeMyTool(argsJson),
            _         => HostResponse.Fail($"unknown tool: {toolName}")
        };
    }

    public HostResponse Destroy()
    {
        // TODO: 释放资源
        return HostResponse.Ok(null);
    }

    private HostResponse InvokeMyTool(string? argsJson)
    {
        // argsJson 是宿主传来的参数 JSON，按需反序列化
        // 返回值是字符串（可以是纯文本或 JSON）
        return HostResponse.Ok("工具执行结果");
    }
}
```

---

## 七、plugin.json 样例

```json
{
  "id": "my-plugin",
  "name": "我的插件",
  "version": "1.0.0",
  "description": "插件功能描述",
  "runtime": "process",
  "command": "MyPlugin.exe"
}
```

字段说明：

| 字段 | 必须 | 说明 |
|------|------|------|
| id | ✓ | 唯一标识，建议小写 kebab-case |
| name | ✓ | 显示名称 |
| version | ✓ | 版本号 |
| runtime | ✓ | 固定填 `"process"` |
| command | ✓ | 插件目录下的 exe 文件名（相对路径） |

---

## 八、发布命令

### JIT self-contained（推荐，无 AOT 约束）

```powershell
dotnet publish MyPlugin.csproj `
  -c Release `
  -r win-x64 `
  --self-contained `
  /p:PublishSingleFile=true `
  -o publish/
```

产物 `publish/MyPlugin.exe` 约 60-100 MB，包含 .NET Runtime，目标机器无需安装 .NET。

### AOT exe（启动更快，体积更小）

```powershell
dotnet publish MyPlugin.csproj `
  -c Release `
  -r win-x64 `
  /p:PublishAot=true `
  -o publish/
```

AOT 约束：禁止动态反射、Emit、MakeGenericType（运行时未知类型）。  
所有序列化类型须注册到 `PluginJsonContext`。详见 native 子技能 AOT Rules。

---

## 九、安装到插件目录

```text
%AppData%\Cortana\plugins\my-plugin\
├── plugin.json
└── MyPlugin.exe
```

运行时安装（已运行中的插件更新）请切换到 publish-install 子技能，
使用 sys_unload_plugin → zip 安装 → sys_reload_plugin 流程。

