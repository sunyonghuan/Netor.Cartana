<#
.SYNOPSIS
    AOT 发布 Process 子进程插件并部署到插件目录。
.DESCRIPTION
    1. AOT 编译 C# EXE 插件（framework-dependent）
    2. 部署 EXE + plugin.json + 依赖 DLL 到 Cortana 插件目录
.PARAMETER ProjectDir
    插件项目相对于仓库根目录的路径，如 Samples\MyPlugin。
.PARAMETER PluginName
    插件部署目录名（默认使用项目名的 kebab-case）。
.PARAMETER PluginRoot
    自定义插件部署根目录（默认部署到 Debug 输出）。
.PARAMETER FrameworkDependent
    关闭自包含发布（需目标机器已装 .NET 10，仅 非 AOT 项目可用）。
    默认自包含，因为 AOT 必须 self-contained=true。
#>
param(
    [Parameter(Mandatory=$true)][string]$ProjectDir,
    [string]$PluginName,
    [string]$PluginRoot,
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'
# $PSScriptRoot = <repo>\.github\skills\plugin-development\scripts；向上 4 级 = 仓库根
$SolutionDir = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$FullProjectDir = Join-Path $SolutionDir $ProjectDir

if (-not (Test-Path $FullProjectDir)) {
    Write-Host "❌ 项目目录不存在：$FullProjectDir" -ForegroundColor Red
    exit 1
}

$csproj = Get-ChildItem -Path $FullProjectDir -Filter "*.csproj" | Select-Object -First 1
if (-not $csproj) {
    Write-Host "❌ 未找到 .csproj 文件" -ForegroundColor Red
    exit 1
}

$ProjectName = $csproj.BaseName
if (-not $PluginName) {
    $PluginName = ($ProjectName -creplace '([A-Z])', '-$1').Trim('-').ToLower()
}

$OutDir = Join-Path $FullProjectDir "bin\publish"
$DeployTargets = @()
if ($PluginRoot) {
    $DeployTargets += $PluginRoot
} else {
    $DeployTargets += Join-Path $SolutionDir "Src\Netor.Cortana\bin\Debug\net10.0-windows\.cortana\plugins"
}

Write-Host "=== Process 插件 AOT 发布: $ProjectName ===" -ForegroundColor Cyan

$scFlag = if ($FrameworkDependent) { "false" } else { "true" }
Write-Host "`n[1/2] AOT 编译 (self-contained=$scFlag)..." -ForegroundColor Yellow
dotnet publish $csproj.FullName -c Release -r win-x64 --self-contained $scFlag -o $OutDir --nologo -v quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ AOT 编译失败" -ForegroundColor Red
    exit 1
}

Write-Host "[2/2] 部署文件..." -ForegroundColor Yellow

# 查找 EXE（AssemblyName 可能与 ProjectName 不同）
$exe = Get-ChildItem -Path $OutDir -Filter "*.exe" | Select-Object -First 1
if (-not $exe) {
    Write-Host "❌ 发布产出没有 .exe" -ForegroundColor Red
    exit 1
}

$projJson = Join-Path $FullProjectDir "plugin.json"
if (-not (Test-Path $projJson)) {
    Write-Host "❌ 项目根目录缺 plugin.json" -ForegroundColor Red
    exit 1
}

foreach ($target in $DeployTargets) {
    $targetDir = Join-Path $target $PluginName
    if (Test-Path $targetDir) {
        Remove-Item $targetDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

    # 复制 exe + plugin.json + 所有运行时必需文件（除 .pdb / .xml）
    Get-ChildItem $OutDir -File | Where-Object {
        $_.Extension -notin '.pdb', '.xml'
    } | ForEach-Object {
        Copy-Item $_.FullName $targetDir
    }
    Copy-Item $projJson $targetDir -Force

    Write-Host "  ✓ $targetDir" -ForegroundColor Green
}

$exeSize = $exe.Length / 1KB
Write-Host "`n=== 发布完成 ===" -ForegroundColor Green
Write-Host "EXE: $($exe.Name) ($([math]::Round($exeSize, 1)) KB)"
Write-Host "部署到 $($DeployTargets.Count) 个目录"
