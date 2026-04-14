---
name: file-transfer-subskill
description: 文件上传下载
version: 1.1.0
---

# 文件传输子技能

## 功能描述
在本地和服务器之间上传/下载文件。

## 使用场景
- 部署应用文件
- 备份服务器数据
- 日志文件下载分析
- 配置文件更新
- **SSH 密钥文件下载（关键场景）**

## 传输方式
- **SCP（推荐，保留原始格式）**
- SFTP
- rsync（大文件）

## 支持操作
- 单文件上传/下载
- 批量文件传输
- 文件夹同步
- 断点续传

---

## 🔑 SSH 密钥文件下载规范（关键）

### ⚠️ 重要警告

**SSH 私钥文件对格式极其敏感**，错误的下载方式会导致 `invalid format` 错误，无法用于认证。

### ❌ 错误做法（禁止使用）

```powershell
# 错误 1：通过 SSH 会话 cat 输出，再写入文件
ssh root@server "cat /root/.ssh/id_rsa"
# 然后将输出内容用文本方式写入文件
# 问题：换行符会从 LF 变成 CRLF，导致密钥格式错误
```

```powershell
# 错误 2：使用 base64 编码再解码
ssh root@server "base64 /root/.ssh/id_rsa"
# 然后在本地解码
# 问题：可能引入编码错误，格式损坏
```

**为什么错误？**
- Windows 文本写入会自动转换换行符（`\n` → `\r\n`）
- OpenSSH 私钥对换行符和编码极其敏感
- CRLF 换行符会导致 `Load key "xxx": invalid format` 错误

### ✅ 正确做法（必须使用）

#### 方法 A：直接使用 SCP 下载（推荐）⭐

```powershell
# 语法
scp -P <端口> <用户>@<服务器 IP>:<远程路径> <本地路径>

# 示例：下载 SSH 私钥
scp -P 22 root@10.10.10.5:/root/.ssh/id_rsa_new "E:\Workspace\Servers\10.10.10.5\id_rsa"
```

**优点：**
- ✅ 二进制传输，保留原始格式
- ✅ 换行符不变（保持 Linux LF 格式）
- ✅ 无编码转换风险
- ✅ 简单直接

#### 方法 B：使用 SSH 重定向（备选）

```powershell
# 语法
ssh <用户>@<服务器 IP> "cat <远程路径>" > <本地路径>

# 示例
ssh root@10.10.10.5 "cat /root/.ssh/id_rsa_new" > "E:\Workspace\Servers\10.10.10.5\id_rsa"
```

**注意：**
- ⚠️ 必须确保 PowerShell 不进行文本转换
- ⚠️ 某些情况下仍可能改变换行符
- ⚠️ 不如 SCP 可靠

---

### 📋 密钥下载完整流程

#### 步骤 1：确认服务器端密钥位置
```powershell
# 先通过密码登录服务器
ssh root@10.10.10.5

# 查看密钥文件
ls -la /root/.ssh/
# 确认文件名（如 id_rsa、id_rsa_new 等）
```

#### 步骤 2：使用 SCP 下载密钥
```powershell
# 直接下载（推荐）
scp -P 22 root@10.10.10.5:/root/.ssh/id_rsa_new "E:\Workspace\Servers\10.10.10.5\id_rsa"
```

#### 步骤 3：验证下载结果
```powershell
# 检查文件大小
Get-Item "E:\Workspace\Servers\10.10.10.5\id_rsa" | Select-Object Length

# 检查文件格式（应显示 OPENSSH PRIVATE KEY）
Get-Content "E:\Workspace\Servers\10.10.10.5\id_rsa" -Head 1

# 检查换行符（应为纯 LF）
$bytes = [System.IO.File]::ReadAllBytes("E:\Workspace\Servers\10.10.10.5\id_rsa")
$hasCRLF = $bytes -contains 13  # 13 = CR (\r)
if ($hasCRLF) { Write-Host "警告：发现 CRLF 换行符！" } else { Write-Host "✓ 换行符正确（纯 LF）" }
```

#### 步骤 4：修复本地权限（Windows 必需）
```powershell
# 断开继承
$acl = Get-Acl "E:\Workspace\Servers\10.10.10.5\id_rsa"
$acl.SetAccessRuleProtection($true, $false)

# 添加 Administrator 和 SYSTEM 完全控制
$rule1 = New-Object System.Security.AccessControl.FileSystemAccessRule("Administrator", "FullControl", "Allow")
$rule2 = New-Object System.Security.AccessControl.FileSystemAccessRule("SYSTEM", "FullControl", "Allow")
$acl.AddAccessRule($rule1)
$acl.AddAccessRule($rule2)

# 应用权限
Set-Acl "E:\Workspace\Servers\10.10.10.5\id_rsa" $acl
```

#### 步骤 5：测试密钥连接
```powershell
# 详细模式测试
ssh -v -i "E:\Workspace\Servers\10.10.10.5\id_rsa" root@10.10.10.5

# 确认输出中没有 "invalid format" 错误
# 确认有 "Authenticated with public key" 或类似成功信息
```

---

### 🚨 常见错误与解决方案

| 错误信息 | 原因 | 解决方案 |
|----------|------|----------|
| `Load key "xxx": invalid format` | 换行符错误（CRLF）或格式损坏 | **重新用 SCP 下载**，不要文本写入 |
| `Permission denied (publickey)` | 密钥权限错误或服务器端 authorized_keys 不匹配 | 检查本地权限 + 验证服务器端公钥 |
| `Connection timed out` | 网络问题或服务器无响应 | 检查网络连接 + 服务器状态 |
| `WARNING: UNPROTECTED PRIVATE KEY FILE!` | 权限过于开放（Linux） | `chmod 600 id_rsa` |

---

## 安全注意
- 验证文件完整性（MD5/SHA256）
- 传输加密
- 权限设置（密钥文件必须限制访问）
- 敏感文件保护
- **密钥文件永远不要用文本方式传输或编辑**

---

## 版本历史
- **v1.1.0** (2026-04-10): 添加 SSH 密钥下载规范，强调必须使用 SCP 直接下载
- v1.0.0: 初始版本
