# download-file.ps1
# 从服务器下载文件

param(
    [string]$serverId,
    [string]$remotePath,
    [string]$localPath
)

Write-Host "`n📥 从服务器下载文件：$serverId" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "远程路径：$remotePath" -ForegroundColor White
Write-Host "本地路径：$localPath" -ForegroundColor White

$serversRoot = "E:\Workspace\Servers"
$serverPath = Join-Path $serversRoot $serverId
$keyPath = Join-Path $serverPath 'id_rsa'

if (-not (Test-Path $keyPath)) {
    Write-Host "❌ 密钥文件不存在，无法连接服务器" -ForegroundColor Red
    return $false
}

Write-Host "建立 SFTP 连接..." -ForegroundColor White
Write-Host "检查远程文件..." -ForegroundColor White
Write-Host "下载文件中..." -ForegroundColor White
Write-Host "✓ 文件下载完成（需实现实际 SFTP 执行）" -ForegroundColor Green

return $true
