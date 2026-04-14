---
name: server-maintainer
description: '服务器维护入口技能。用于在固定工作区下管理服务器清单、执行密钥优先认证、生成巡检报表和分流常见维护任务。'
user-invocable: true
---

# Server Maintainer

## Purpose

- 统一管理工作区下的 Servers 和 Reports。
- 在服务器信息完整时避免重复追问。
- 强制执行密钥优先、密码兜底的认证规则。
- 将维护任务分流到 inventory、auth、reporting、operations、transfer。

## Workspace Rules

- 服务器目录固定为工作区下的 Servers/{IP}/。
- 报表目录固定为工作区下的 Reports/。
- server.md、id_rsa、日报表都按固定命名存放，不手工发明新路径。

## Authentication Rules

- 只要 Servers/{IP}/id_rsa 存在，就直接走密钥认证，不再询问密码。
- 只有首次接入且本地没有密钥时，才允许进入密码兜底流程。
- 密码不写入脚本参数、文件、日志、报表。
- 若密钥存在但连接失败，先检查权限和配置，再决定是否重新生成密钥。

## Routing

- 新增服务器、列清单、修正 server.md：inventory
- 连接、鉴权、初始化密钥、修权限：auth
- 巡检、阈值判断、日报：reporting
- 清理、安装、服务维护：operations
- 上传、下载、覆盖确认：transfer

## Maintenance Policy

- 只读检查可直接执行。
- 会修改服务器状态的操作必须先复述目标服务器、目标路径或目标服务。
- 不可逆或高风险操作默认不执行，除非用户明确确认。

## Shared Assets

- resources/server-info-template.md
- resources/monitor-thresholds.md
- resources/server-record-schema.md
- resources/auth-policy.md
- resources/report-template.md
- resources/operation-matrix.md
- resources/maintenance-checklist.md
- resources/transfer-policy.md

## Shared Scripts

- scripts/ServerMaintainer.Common.ps1
- scripts/list-servers.ps1
- scripts/validate-server-info.ps1
- scripts/connect-server.ps1
- scripts/setup-server-key.ps1
- scripts/generate-monitor-report.ps1