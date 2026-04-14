#!/usr/bin/env pwsh
# Cortana AvaloniaUI — Native AOT 发布脚本
# 输出目录: .\Realases\AvaloniaUI

$ErrorActionPreference = 'Stop'

$SolutionDir = $PSScriptRoot
$ProjectFile = Join-Path $SolutionDir 'Src\Netor.Cortana.AvaloniaUI\Netor.Cortana.AvaloniaUI.csproj'
$NativeHostProjectFile = Join-Path $SolutionDir 'Src\Plugins\Netor.Cortana.NativeHost\Netor.Cortana.NativeHost.csproj'
$OutputDir = Join-Path $SolutionDir 'Realases\AvaloniaUI'

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Cortana AvaloniaUI — Native AOT Publish" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "项目: $ProjectFile"
Write-Host "宿主: $NativeHostProjectFile"
Write-Host "输出: $OutputDir"
Write-Host ""

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$running = Get-Process -Name 'Cortana' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[*] 终止正在运行的 Cortana 进程..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

$cleanExts = @('*.exe','*.dll','*.json','*.config','*.manifest')
foreach ($ext in $cleanExts) {
    Get-ChildItem -Path $OutputDir -Filter $ext -File -ErrorAction SilentlyContinue |
        Remove-Item -Force
}

Write-Host "[1/3] 正在发布 AvaloniaUI (Release | win-x64 | AOT)..." -ForegroundColor Green

dotnet publish $ProjectFile `
    -c Release `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "发布失败！退出码: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "[2/3] 正在发布 NativeHost (Release | win-x64 | AOT)..." -ForegroundColor Green

dotnet publish $NativeHostProjectFile `
    -c Release `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "NativeHost 发布失败！退出码: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "[3/3] 清理多余文件..." -ForegroundColor Green
$junkExts = @('*.pdb','*.xml','*.deps.json','*.runtimeconfig.dev.json')
foreach ($ext in $junkExts) {
    Get-ChildItem -Path $OutputDir -Filter $ext -File -ErrorAction SilentlyContinue |
        Remove-Item -Force
}

$exePath = Join-Path $OutputDir 'Cortana.exe'
$nativeHostExePath = Join-Path $OutputDir 'Cortana.NativeHost.exe'
if (Test-Path $exePath) {
    $size = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
    Write-Host ""
    Write-Host "发布完成！" -ForegroundColor Green
    Write-Host "  路径: $exePath"
    Write-Host "  大小: ${size} MB"

    if (Test-Path $nativeHostExePath) {
        $nativeHostSize = [math]::Round((Get-Item $nativeHostExePath).Length / 1MB, 2)
        Write-Host "  宿主: $nativeHostExePath"
        Write-Host "  宿主大小: ${nativeHostSize} MB"
    } else {
        Write-Host "警告: 未找到 Cortana.NativeHost.exe，请检查发布日志。" -ForegroundColor Yellow
    }
} else {
    Write-Host "警告: 未找到 Cortana.exe，请检查发布日志。" -ForegroundColor Yellow
}