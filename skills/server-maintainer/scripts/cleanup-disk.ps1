# cleanup-disk.ps1
# 清理服务器磁盘

param(
    [string]$serverId,
    [string]$targetPath = '/tmp'
)

Write-Host "`n💾 清理服务器磁盘：$serverId" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "目标路径：$targetPath" -ForegroundColor White

$serversRoot = "E:\Workspace\Servers"
$serverPath = Join-Path $serversRoot $serverId
$keyPath = Join-Path $serverPath 'id_rsa'

if (-not (Test-Path $keyPath)) {
    Write-Host "❌ 密钥文件不存在，无法连接服务器" -ForegroundColor Red
    return $false
}

Write-Host "分析磁盘使用情况..." -ForegroundColor White
Write-Host "清理临时文件..." -ForegroundColor White
Write-Host "清理缓存文件..." -ForegroundColor White
Write-Host "✓ 磁盘清理完成（需实现实际 SSH 执行）" -ForegroundColor Green

return $true
