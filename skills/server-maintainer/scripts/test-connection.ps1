# test-connection.ps1
# 测试服务器连接

param(
    [string]$serverId,
    [int]$timeout = 30
)

$serversRoot = "E:\Workspace\Servers"
$serverPath = Join-Path $serversRoot $serverId

Write-Host "`n🔌 测试服务器连接：$serverId" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

if (-not (Test-Path $serverPath)) {
    Write-Host "❌ 服务器文件夹不存在：$serverPath" -ForegroundColor Red
    return $false
}

$keyPath = Join-Path $serverPath 'id_rsa'
$serverMd = Join-Path $serverPath 'server.md'

# 检查密钥文件
if (Test-Path $keyPath) {
    Write-Host "✓ 密钥文件存在" -ForegroundColor Green
} else {
    Write-Host "⚠️ 密钥文件不存在" -ForegroundColor Yellow
}

# 检查信息文件
if (Test-Path $serverMd) {
    Write-Host "✓ 信息文件存在" -ForegroundColor Green
} else {
    Write-Host "⚠️ 信息文件不存在" -ForegroundColor Yellow
}

# 测试连接（需要实际 SSH 实现）
Write-Host "`n正在测试 SSH 连接..." -ForegroundColor White
Write-Host "⏱️  超时时间：${timeout}秒" -ForegroundColor White

# 模拟连接测试
Start-Sleep -Seconds 2
Write-Host "✓ 连接测试完成（需实现实际 SSH 连接）" -ForegroundColor Green

return $true
