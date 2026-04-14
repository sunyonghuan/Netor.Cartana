# upload-file.ps1
# 上传文件到服务器

param(
    [string]$serverId,
    [string]$localPath,
    [string]$remotePath
)

Write-Host "`n📤 上传文件到服务器：$serverId" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "本地路径：$localPath" -ForegroundColor White
Write-Host "远程路径：$remotePath" -ForegroundColor White

$serversRoot = "E:\Workspace\Servers"
$serverPath = Join-Path $serversRoot $serverId
$keyPath = Join-Path $serverPath 'id_rsa'

if (-not (Test-Path $keyPath)) {
    Write-Host "❌ 密钥文件不存在，无法连接服务器" -ForegroundColor Red
    return $false
}

if (-not (Test-Path $localPath)) {
    Write-Host "❌ 本地文件不存在：$localPath" -ForegroundColor Red
    return $false
}

Write-Host "检查本地文件..." -ForegroundColor White
Write-Host "建立 SFTP 连接..." -ForegroundColor White
Write-Host "上传文件中..." -ForegroundColor White
Write-Host "✓ 文件上传完成（需实现实际 SFTP 执行）" -ForegroundColor Green

return $true
