name: dotnet
description: 'Cortana Dotnet 插件开发子技能。位置：plugin-development/subskills/dotnet。该通道已废弃，仅用于存量 IPlugin/plugin.json 插件维护、迁移和兼容处理。触发关键词：Dotnet 插件、IPlugin、托管插件、plugin.json、历史插件迁移。'
user-invocable: true
---

# Plugin Development Dotnet Deprecated

## Status

- Dotnet 通道已废弃，不作为新插件开发默认方案。
- 新需求优先转 Native；跨语言或外部工具集成优先转 MCP。
- 只有在维护存量插件、修复兼容问题、执行迁移收口时才进入本子技能。

## Flow

1. 先确认是否必须继续保留 Dotnet 插件；能迁移则优先迁移到 Native 或 MCP。
2. 需要维护历史插件时再使用 create-dotnet-plugin.ps1 或现有项目结构。
3. 入口类实现 IPlugin，只暴露 AITool。
4. 组合根集中注册服务，按 architecture 子技能约束实现。
5. publish-dotnet-plugin.ps1；需要安装包时加 -CreateZip。
6. 运行时安装切到 publish-install 子技能。

## Rules

- plugin.json 手写并保持和程序集一致。
- 不复制宿主共享程序集到插件目录。
- 局部 DI 只构建一次。
- 工具从容器解析，不在入口类内堆业务。
- 新功能开发前先给出继续保留 Dotnet 的理由；如果没有充分理由，应拒绝并改走 Native 或 MCP。

## Scripts

- scripts/create-dotnet-plugin.ps1
- scripts/publish-dotnet-plugin.ps1

## Resources

- resources/layout.md