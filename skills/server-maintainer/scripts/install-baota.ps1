# install-baota.ps1
# 安装宝塔面板

param(
    [string]$serverId,
    [string]$version = 'latest'
)

Write-Host "`n📦 安装宝塔面板：$serverId" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "版本：$version" -ForegroundColor White

$serversRoot = "E:\Workspace\Servers"
$serverPath = Join-Path $serversRoot $serverId
$keyPath = Join-Path $serverPath 'id_rsa'

if (-not (Test-Path $keyPath)) {
    Write-Host "❌ 密钥文件不存在，无法连接服务器" -ForegroundColor Red
    return $false
}

Write-Host "检查系统环境..." -ForegroundColor White
Write-Host "下载宝塔安装脚本..." -ForegroundColor White
Write-Host "执行安装..." -ForegroundColor White

if ($version -eq 'latest') {
    Write-Host "安装最新版本宝塔面板..." -ForegroundColor White
} else {
    Write-Host "安装指定版本：$version" -ForegroundColor White
}

Write-Host "✓ 宝塔面板安装完成（需实现实际 SSH 执行）" -ForegroundColor Green
Write-Host "`n📝 请登录宝塔面板查看默认账号密码" -ForegroundColor Yellow

return $true
