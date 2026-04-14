---
name: auth
description: '服务器认证与密钥治理子技能。用于密钥优先连接、密钥初始化、权限修复、密码兜底条件控制。'
user-invocable: true
---

# Server Auth

## Scope

- 检查并决定认证方式。
- 初始化 SSH 密钥。
- 修复 Windows 私钥权限。
- 约束何时允许询问密码。

## Rules

- 只要 Servers/{IP}/id_rsa 存在，就禁止再次询问 SSH 密码。
- 只有首次接入且本地不存在密钥时，才允许进入密码兜底流程。
- 密码不能写入文件、脚本参数、日志或报表。
- 密钥权限异常时先执行 fix-key-permission.ps1，再决定是否要求重新配置。

## Scripts

- scripts/connect-server.ps1
- scripts/setup-server-key.ps1
- scripts/fix-key-permission.ps1

## Resources

- resources/auth-policy.md