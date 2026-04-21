---
title: C# AOT EXE 子进程插件模板
version: 1
---

# C# Process 子进程插件模板

最小可编译的 C# AOT EXE 插件，演示完整 NDJSON 协议循环。

## 文件清单

- [Template.csproj](./Template.csproj) — AOT EXE 工程
- [Program.cs](./Program.cs) — 主循环 + 方法分派
- [Protocol.cs](./Protocol.cs) — 请求/响应 record + JsonSerializerContext
- [plugin.json](./plugin.json) — 清单

## 如何使用

```powershell
# 复制整个目录，改名，然后发布：
dotnet publish -c Release -r win-x64 --self-contained false -o bin\publish
```

产物是 `<AssemblyName>.exe`，放到 `.cortana/plugins/<插件名>/` 并保留 `plugin.json`。

## 修改点

1. 改 `Template.csproj` 的 `<AssemblyName>` 和 `<RootNamespace>`
2. 改 `plugin.json` 的 `id` / `name` / `command`
3. 在 `Program.cs` 的 `HandleInvoke` 里添加自己的工具分支
4. 在 `Protocol.cs` 里为每个工具的参数类型加 `[JsonSerializable]`
