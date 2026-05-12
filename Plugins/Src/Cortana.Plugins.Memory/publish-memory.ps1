# publish-memory.ps1 - 增强记忆引擎插件单独发布脚本
# 自动递增版本号 -> 修改 csproj + Startup.cs -> dotnet publish -> 打包 zip 到仓库 Releases 目录
param(
    [switch]$DryRun,
    [ValidateSet("patch", "minor", "major")]
    [string]$Bump = "patch",
    [string]$Runtime = "win-x64"
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($true)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($true)
$OutputEncoding = [Console]::OutputEncoding

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
$Root = Resolve-Path (Join-Path $ProjectDir "..\..")
$ReleasesDir = Join-Path $Root "Releases"
$ProjectName = "Cortana.Plugins.Memory"
$FriendlyName = "Memory"
$CsprojPath = Join-Path $ProjectDir "$ProjectName.csproj"

if (-not (Test-Path $CsprojPath)) {
    Write-Host "  [ERROR] 项目文件不存在: $CsprojPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $ReleasesDir)) {
    New-Item -ItemType Directory -Path $ReleasesDir | Out-Null
}

function Step-Version([string]$ver, [string]$part) {
    $parts = $ver.Split('.')
    if ($parts.Length -lt 3) { $parts = @("1", "0", "0") }
    $major = [int]$parts[0]; $minor = [int]$parts[1]; $patch = [int]$parts[2]
    switch ($part) {
        "major" { $major++; $minor = 0; $patch = 0 }
        "minor" { $minor++; $patch = 0 }
        "patch" { $patch++ }
    }
    return "$major.$minor.$patch"
}

function Get-CsprojVersion([string]$csprojPath) {
    $xml = [xml]([System.IO.File]::ReadAllText($csprojPath))
    $node = $xml.SelectSingleNode("//PropertyGroup/Version")
    if ($node) { return $node.InnerText }
    return "0.0.0"
}

function Set-CsprojVersion([string]$csprojPath, [string]$newVer) {
    $content = [System.IO.File]::ReadAllText($csprojPath)
    $content = $content -replace '<Version>[^<]+</Version>', "<Version>$newVer</Version>"
    [System.IO.File]::WriteAllText($csprojPath, $content, [System.Text.UTF8Encoding]::new($true))
}

function Set-StartupVersion([string]$projectDir, [string]$newVer) {
    $startupFiles = Get-ChildItem -Path $projectDir -Filter "Startup.cs" -Recurse
    foreach ($f in $startupFiles) {
        $content = [System.IO.File]::ReadAllText($f.FullName)
        if ($content -match 'Version\s*=\s*"[^"]*"') {
            $content = $content -replace 'Version\s*=\s*"[^"]*"', "Version = `"$newVer`""
            [System.IO.File]::WriteAllText($f.FullName, $content, [System.Text.UTF8Encoding]::new($true))
            Write-Host "    Startup.cs Version -> $newVer" -ForegroundColor DarkGray
        }
    }
}

Write-Host ""
Write-Host "  Cortana Memory Plugin Publisher" -ForegroundColor Cyan
Write-Host "  Project: $ProjectName" -ForegroundColor DarkGray
Write-Host "  Bump: $Bump | Runtime: $Runtime | DryRun: $DryRun" -ForegroundColor DarkGray
Write-Host ""

$oldVer = Get-CsprojVersion $CsprojPath
$newVer = Step-Version $oldVer $Bump
Write-Host "  版本: $oldVer -> $newVer" -ForegroundColor White

if ($DryRun) {
    Write-Host "  [DRY-RUN] 跳过实际操作" -ForegroundColor DarkYellow
    exit 0
}

Set-CsprojVersion $CsprojPath $newVer
Set-StartupVersion $ProjectDir $newVer

$publishDir = Join-Path $ProjectDir "bin\publish"
Write-Host "  发布中..." -ForegroundColor DarkGray

$publishOutput = dotnet publish $CsprojPath `
    -c Release `
    -r $Runtime `
    -o $publishDir `
    2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "  [FAILED] dotnet publish 失败" -ForegroundColor Red
    Write-Host $publishOutput -ForegroundColor DarkRed
    exit $LASTEXITCODE
}

$stageDir = Join-Path ([System.IO.Path]::GetTempPath()) "cortana_publish_$FriendlyName"
$pluginFolder = Join-Path $stageDir $FriendlyName
if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
New-Item -ItemType Directory -Path $pluginFolder | Out-Null

Get-ChildItem -Path $publishDir -File | Where-Object {
    $_.Extension -ne ".pdb"
} | ForEach-Object {
    Copy-Item $_.FullName -Destination $pluginFolder
}

$zipName = "$FriendlyName.v$newVer.zip"
$zipPath = Join-Path $ReleasesDir $zipName

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $pluginFolder -DestinationPath $zipPath -CompressionLevel Optimal

Remove-Item $stageDir -Recurse -Force
Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue

$zipSize = [math]::Round((Get-Item $zipPath).Length / 1KB, 1)
Write-Host "  OK $zipName ($($zipSize) KB)" -ForegroundColor Green
Write-Host "  输出: $zipPath" -ForegroundColor Cyan
Write-Host ""
