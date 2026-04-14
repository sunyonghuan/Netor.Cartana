---
name: transfer
description: '服务器文件传输子技能。用于上传、下载、路径确认、覆盖策略和回传结果。'
user-invocable: true
---

# Server Transfer

## Scope

- 上传文件到服务器。
- 从服务器下载文件。
- 确认远端路径和覆盖策略。

## Rules

- 传输前必须确认源路径和目标路径。
- 默认不覆盖目标文件；需要覆盖时必须明确说明。
- 传输结果要回报文件路径、结果和失败原因。

## Scripts

- scripts/upload-file.ps1
- scripts/download-file.ps1

## Resources

- resources/transfer-policy.md