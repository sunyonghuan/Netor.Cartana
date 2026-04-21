<#
.SYNOPSIS
    创建 C# AOT EXE 子进程插件脚手架（Process 通道）。
.PARAMETER Name
    项目名称（PascalCase），如 MyPlugin。
.PARAMETER Id
    插件 ID（小写字母+数字+下划线），如 my_plugin。
.PARAMETER Description
    插件描述。
.NOTES
    从 references/template-process-csharp 复制并改名。
    工具实现见 Program.cs 的 HandleInvoke。
#>
param(
    [Parameter(Mandatory=$true)][string]$Name,
    [Parameter(Mandatory=$true)][string]$Id,
    [string]$Description = "插件描述"
)

$ErrorActionPreference = 'Stop'

if ($Id -notmatch '^[a-z][a-z0-9_]*$') {
    Write-Host "❌ Id 格式错误：只允许小写字母、数字、下划线，且以字母开头" -ForegroundColor Red
    exit 1
}

# $PSScriptRoot = <repo>\.github\skills\plugin-development\scripts
$SkillDir   = Split-Path -Parent $PSScriptRoot  # plugin-development
$Template   = Join-Path $SkillDir "references\template-process-csharp"
# plugin-development → skills → .github → 仓库根
$RepoRoot   = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $SkillDir))
$ProjectDir = Join-Path $RepoRoot "Samples\$Name"

if (-not (Test-Path $Template)) {
    Write-Host "❌ 找不到模板：$Template" -ForegroundColor Red
    exit 1
}
if (Test-Path $ProjectDir) {
    Write-Host "❌ 目录已存在：$ProjectDir" -ForegroundColor Red
    exit 1
}

Write-Host "=== 创建 Process 插件: $Name ===" -ForegroundColor Cyan

# 1. 复制模板（跳过 README 和 bin/obj）
New-Item -ItemType Directory -Path $ProjectDir -Force | Out-Null
Get-ChildItem $Template -File | Where-Object { $_.Name -ne 'README.md' } | ForEach-Object {
    Copy-Item $_.FullName -Destination (Join-Path $ProjectDir $_.Name)
}

# 2. 计算 kebab-case 可执行文件名
$ExeBase = ($Id -replace '_', '-')
$Namespace = $Name  # PascalCase 作命名空间

# 3. 改写 csproj
$csprojPath = Join-Path $ProjectDir "Template.csproj"
$csprojDest = Join-Path $ProjectDir "$Name.csproj"
$csproj = Get-Content $csprojPath -Raw
$csproj = $csproj.Replace('<RootNamespace>MyProcessPlugin</RootNamespace>', "<RootNamespace>$Namespace</RootNamespace>")
$csproj = $csproj.Replace('<AssemblyName>my-process-plugin</AssemblyName>', "<AssemblyName>$ExeBase</AssemblyName>")
Set-Content -Path $csprojDest -Value $csproj -Encoding UTF8
Remove-Item $csprojPath

# 4. 改写 Program.cs / Protocol.cs 的 namespace + 插件 id
foreach ($cs in @("Program.cs", "Protocol.cs")) {
    $path = Join-Path $ProjectDir $cs
    $text = Get-Content $path -Raw
    $text = $text.Replace('namespace MyProcessPlugin', "namespace $Namespace")
    $text = $text.Replace('using MyProcessPlugin;', "using $Namespace;")
    $text = $text.Replace('my_process_plugin', $Id)
    $text = $text.Replace('my_process_echo', "${Id}_echo")
    Set-Content -Path $path -Value $text -Encoding UTF8
}

# 5. 改写 plugin.json
$pluginJsonPath = Join-Path $ProjectDir "plugin.json"
$pluginJson = Get-Content $pluginJsonPath -Raw
$pluginJson = $pluginJson.Replace('my_process_plugin', $Id)
$pluginJson = $pluginJson.Replace('my-process-plugin.exe', "$ExeBase.exe")
$pluginJson = $pluginJson.Replace('我的子进程插件', $Name)
$pluginJson = $pluginJson.Replace('C# AOT EXE 插件模板', $Description)
Set-Content -Path $pluginJsonPath -Value $pluginJson -Encoding UTF8

Write-Host "✅ 已生成：$ProjectDir" -ForegroundColor Green
Write-Host ""
Write-Host "下一步：" -ForegroundColor Yellow
Write-Host "  1. 在 Program.cs 的 HandleInvoke 添加工具分支" -ForegroundColor Gray
Write-Host "  2. 在 Protocol.cs 为新参数类型加 [JsonSerializable]" -ForegroundColor Gray
Write-Host "  3. dotnet publish -c Release -r win-x64 --self-contained false -o bin\publish" -ForegroundColor Gray
