---
name: reporting
description: '服务器监控与报表子技能。用于采集状态、应用阈值、生成 Markdown 报表并保存到 Reports。'
user-invocable: true
---

# Server Reporting

## Scope

- 批量巡检服务器状态。
- 应用 CPU、内存、硬盘、连接阈值。
- 生成 Markdown 报表并保存到 Reports。

## Rules

- 报表文件名固定为 YYYYMMDD.md。
- 当天重复生成时覆盖当日文件。
- 连接失败的服务器必须继续出现在报表中，状态标记为离线或失败。
- 阈值判断统一读取资源配置，不在多个脚本里重复硬编码。

## Scripts

- scripts/generate-monitor-report.ps1

## Resources

- resources/report-template.md
- resources/alert-rules.md