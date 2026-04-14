# connect-server.ps1
# 建立 SSH 连接（密钥优先，密码兜底）

param(
    [string]$serverId,
    [string]$authType = 'auto'  # auto | key | password
)

$ErrorActionPreference = 'Stop'
. $PSScriptRoot\ServerMaintainer.Common.ps1

Write-Host "`n🔐 建立 SSH 连接：$serverId" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

$serverPath = Get-ServerDirectory -ScriptRoot $PSScriptRoot -ServerId $serverId

if (-not (Test-Path $serverPath)) {
    Write-Host "❌ 服务器文件夹不存在：$serverPath" -ForegroundColor Red
    return $false
}

$serverMd = Join-Path $serverPath 'server.md'
if (-not (Test-Path $serverMd)) {
    Write-Host "❌ 服务器信息文件不存在：$serverMd" -ForegroundColor Red
    return $false
}

$record = Read-ServerRecord -ServerFile $serverMd
$keyPath = $record.KeyPath
$ip = $record.IP
$port = $record.Port
$username = $record.Username

# 密钥优先：自动检测认证方式
if ($authType -eq 'auto') {
    if (Test-ServerKey -ServerRecord $record) {
        $authType = 'key'
        Write-Host "🔑 检测到密钥文件，使用密钥登录" -ForegroundColor Green
    } else {
        Write-Host "⚠️ 未找到密钥文件，当前不进入自动密码流程。" -ForegroundColor Yellow
        Write-Host "请先运行 setup-server-key.ps1 完成首次密钥初始化，或明确指定 -authType password。" -ForegroundColor Yellow
        return $false
    }
}

Write-Host "认证方式：$authType" -ForegroundColor White
Write-Host "服务器信息：" -ForegroundColor White
Write-Host "  IP: $ip" -ForegroundColor White
Write-Host "  端口：$port" -ForegroundColor White
Write-Host "  用户名：$username" -ForegroundColor White

if ($authType -eq 'key') {
    if (Test-Path $keyPath) {
        Write-Host "✓ 密钥文件存在" -ForegroundColor Green
        Write-Host "正在建立 SSH 连接..." -ForegroundColor White
        & ssh -i $keyPath -o BatchMode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=30 -p $port "$username@$ip"
    } else {
        Write-Host "❌ 密钥文件不存在：$keyPath" -ForegroundColor Red
        return $false
    }
} else {
    Write-Host "⚠️ 进入显式密码认证模式，需要用户手动输入密码，脚本不会保存密码。" -ForegroundColor Yellow
    Write-Host "正在建立 SSH 连接..." -ForegroundColor White
    & ssh -o PreferredAuthentications=password -o PubkeyAuthentication=no -o StrictHostKeyChecking=no -o ConnectTimeout=30 -p $port "$username@$ip"
}

return $true
