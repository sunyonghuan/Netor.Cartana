# ============================================================
# 脚本名称：setup-server-key.ps1
# 功能：在远程服务器上配置 SSH 密钥（合并流程，仅需 2 次密码输入）
# 调用时机：添加新服务器时（场景 B：无密钥，使用密码登录）
# 优化说明：将所有服务器端操作合并为单次 SSH 会话，密钥下载用单次 SCP
#           无需安装 sshpass，Windows 原生 SSH 客户端即可使用
# ============================================================

param(
    [Parameter(Mandatory=$true)]
    [string]$ServerIP,

    [Parameter(Mandatory=$false)]
    [int]$Port = 22,

    [Parameter(Mandatory=$true)]
    [string]$Username,

    [Parameter(Mandatory=$true)]
    [string]$WorkspaceDir = ''
)

$ErrorActionPreference = 'Stop'
. $PSScriptRoot\ServerMaintainer.Common.ps1

# 颜色定义
$Green = "Green"
$Red = "Red"
$Yellow = "Yellow"
$Cyan = "Cyan"

Write-Host "========================================" -ForegroundColor $Cyan
Write-Host "  SSH 密钥配置流程（服务器端）" -ForegroundColor $Cyan
Write-Host "  仅需输入 2 次密码" -ForegroundColor $Cyan
Write-Host "========================================" -ForegroundColor $Cyan
Write-Host ""

# 服务器目录路径
if ([string]::IsNullOrWhiteSpace($WorkspaceDir)) {
    $WorkspaceDir = Get-ServerMaintainerWorkspaceRoot -ScriptRoot $PSScriptRoot
}

$ServerDir = Join-Path $WorkspaceDir "Servers\$ServerIP"
$LocalKeyPath = Join-Path $ServerDir "id_rsa"

# 确保服务器目录存在
if (-not (Test-Path $ServerDir)) {
    Write-Host "✗ 服务器目录不存在：$ServerDir" -ForegroundColor $Red
    Write-Host "  请先创建目录结构" -ForegroundColor $Yellow
    exit 1
}

Write-Host "服务器信息:" -ForegroundColor $Cyan
Write-Host "  IP 地址：$ServerIP" -ForegroundColor $Green
Write-Host "  端口号：$Port" -ForegroundColor $Green
Write-Host "  用户名：$Username" -ForegroundColor $Green
Write-Host "  本地目录：$ServerDir" -ForegroundColor $Green
Write-Host ""

# ============================================================
# 步骤 1/4：通过单次 SSH 完成所有服务器端操作
# 包括：检查/修复密钥登录配置、生成密钥对、激活公钥、
#       修复权限、重启 sshd（如需要）
# ============================================================
Write-Host "[步骤 1/4] 执行服务器端配置（单次 SSH 连接）..." -ForegroundColor $Cyan
Write-Host "  ⚡ 请输入 SSH 密码（第 1 次，共 2 次）" -ForegroundColor $Yellow
Write-Host ""

# 构造合并的远程脚本（所有服务器端操作一次完成）
$remoteScript = @'
set -e
NEED_RESTART=0
SSH_DIR="$HOME/.ssh"

# --- 检查/修复 PubkeyAuthentication 配置 ---
if grep -q "^PubkeyAuthentication no" /etc/ssh/sshd_config 2>/dev/null; then
    echo "[CONFIG] PubkeyAuthentication 已禁用，正在启用..."
    sudo sed -i 's/^PubkeyAuthentication no/PubkeyAuthentication yes/' /etc/ssh/sshd_config
    NEED_RESTART=1
    echo "[CONFIG] ✓ 已修改为 PubkeyAuthentication yes"
elif grep -q "^PubkeyAuthentication yes" /etc/ssh/sshd_config 2>/dev/null; then
    echo "[CONFIG] ✓ PubkeyAuthentication 已启用"
else
    echo "[CONFIG] ⚠ 未找到 PubkeyAuthentication 配置，使用默认值（启用）"
fi

# --- 生成新密钥对（ed25519 算法）---
echo "[KEYGEN] 生成 ed25519 密钥对..."
mkdir -p "$SSH_DIR"
rm -f "$SSH_DIR/id_rsa_new" "$SSH_DIR/id_rsa_new.pub"
ssh-keygen -t ed25519 -f "$SSH_DIR/id_rsa_new" -N "" -q
echo "[KEYGEN] ✓ 密钥对生成成功"

# --- 激活公钥 ---
echo "[PUBKEY] 激活公钥到 authorized_keys..."
cat "$SSH_DIR/id_rsa_new.pub" >> "$SSH_DIR/authorized_keys"
echo "[PUBKEY] ✓ 公钥已激活"

# --- 修复服务器端权限 ---
echo "[PERM] 修复 .ssh 目录权限..."
chmod 700 "$SSH_DIR"
chmod 600 "$SSH_DIR/authorized_keys"
chown -R "$(id -un)":"$(id -gn)" "$SSH_DIR"
echo "[PERM] ✓ 权限修复完成"

# --- 重启 sshd（仅在修改了配置时）---
if [ "$NEED_RESTART" = "1" ]; then
    echo "[SSHD] 重启 SSH 服务..."
    sudo systemctl restart sshd
    echo "[SSHD] ✓ SSH 服务已重启"
fi

echo ""
echo "SETUP_REMOTE_OK"
'@

try {
    $result = ssh -o StrictHostKeyChecking=no -p $Port "$Username@$ServerIP" $remoteScript 2>&1
    $resultText = $result -join "`n"

    # 打印远程输出
    foreach ($line in $result) {
        if ($line -match "^\[") {
            Write-Host "  $line" -ForegroundColor $Green
        }
    }

    if ($resultText -match "SETUP_REMOTE_OK") {
        Write-Host "" 
        Write-Host "✓ 服务器端配置全部完成" -ForegroundColor $Green
    } else {
        Write-Host "" 
        Write-Host "✗ 服务器端配置可能未完成，请检查输出" -ForegroundColor $Red
        Write-Host "  完整输出：" -ForegroundColor $Yellow
        Write-Host $resultText -ForegroundColor $Yellow
        exit 1
    }
} catch {
    Write-Host "✗ SSH 连接失败：$_" -ForegroundColor $Red
    exit 1
}

Write-Host ""

# ============================================================
# 步骤 2/4：通过单次 SCP 下载密钥到本地
# ============================================================
Write-Host "[步骤 2/4] 下载密钥到本地（SCP）..." -ForegroundColor $Cyan
Write-Host "  源路径：/root/.ssh/id_rsa_new" -ForegroundColor $Cyan
Write-Host "  目标路径：$LocalKeyPath" -ForegroundColor $Cyan
Write-Host "  ⚡ 请输入 SSH 密码（第 2 次，共 2 次）" -ForegroundColor $Yellow
Write-Host ""

try {
    scp -o StrictHostKeyChecking=no -P $Port "${Username}@${ServerIP}:~/.ssh/id_rsa_new" "$LocalKeyPath"

    if (Test-Path $LocalKeyPath) {
        # 验证密钥格式
        $firstLine = Get-Content $LocalKeyPath -TotalCount 1
        if ($firstLine -match "BEGIN.*PRIVATE KEY") {
            Write-Host "✓ 密钥下载成功，格式验证通过" -ForegroundColor $Green
        } else {
            Write-Host "⚠ 密钥已下载，但格式可能异常（首行：$firstLine）" -ForegroundColor $Yellow
        }
    } else {
        Write-Host "✗ 密钥文件未下载到本地" -ForegroundColor $Red
        exit 1
    }
} catch {
    Write-Host "✗ SCP 下载失败：$_" -ForegroundColor $Red
    exit 1
}

Write-Host ""

# ============================================================
# 步骤 3/4：修复本地密钥权限（无需密码）
# ============================================================
Write-Host "[步骤 3/4] 修复本地密钥权限..." -ForegroundColor $Cyan

# 调用 fix-key-permission.ps1 脚本
$fixPermissionScript = Join-Path $PSScriptRoot "fix-key-permission.ps1"

if (Test-Path $fixPermissionScript) {
    try {
        & $fixPermissionScript -FilePath $LocalKeyPath
        Write-Host "✓ 本地密钥权限修复完成" -ForegroundColor $Green
    } catch {
        Write-Host "⚠ 权限修复脚本执行失败：$_" -ForegroundColor $Yellow
        Write-Host "  请手动执行权限修复" -ForegroundColor $Yellow
    }
} else {
    Write-Host "⚠ 未找到权限修复脚本，跳过此步骤" -ForegroundColor $Yellow
}

Write-Host ""

# ============================================================
# 步骤 4/4：验证密钥连接（无需密码）
# ============================================================
Write-Host "[步骤 4/4] 验证密钥连接..." -ForegroundColor $Cyan

$testCommand = 'echo "SSH_KEY_AUTH_OK"'
try {
    $testResult = ssh -i "$LocalKeyPath" -o StrictHostKeyChecking=no -p $Port "$Username@$ServerIP" $testCommand 2>&1
    $testText = $testResult -join "`n"

    if ($testText -match "SSH_KEY_AUTH_OK") {
        Write-Host "✓ 密钥连接验证成功！后续连接无需密码" -ForegroundColor $Green
    } else {
        Write-Host "⚠ 连接测试输出：$testText" -ForegroundColor $Yellow
        Write-Host "  密钥连接可能存在问题，请检查权限" -ForegroundColor $Yellow
    }
} catch {
    Write-Host "✗ 密钥连接验证失败：$_" -ForegroundColor $Red
    Write-Host "  请检查密钥权限和服务器配置" -ForegroundColor $Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor $Cyan
Write-Host "  SSH 密钥配置流程完成！" -ForegroundColor $Green
Write-Host "========================================" -ForegroundColor $Cyan
Write-Host ""
Write-Host "本地密钥路径：$LocalKeyPath" -ForegroundColor $Cyan
Write-Host "下一步：生成 server.md 文件并更新系统信息" -ForegroundColor $Cyan
Write-Host ""
