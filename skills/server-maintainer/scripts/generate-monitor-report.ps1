# generate-monitor-report.ps1
# 生成服务器监控报表

param(
    [array]$servers
)

$ErrorActionPreference = 'Continue'
. $PSScriptRoot\ServerMaintainer.Common.ps1

Write-Host "`n服务器监控报表 - $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

$reportsRoot = Get-ReportsRoot -ScriptRoot $PSScriptRoot
if (-not (Test-Path $reportsRoot)) {
    New-Item -Path $reportsRoot -ItemType Directory -Force | Out-Null
}

if (-not $servers) {
    $serversRoot = Get-ServersRoot -ScriptRoot $PSScriptRoot
    $servers = Get-ChildItem -Path $serversRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $serverMd = Join-Path $_.FullName 'server.md'
        if (Test-Path $serverMd) {
            Read-ServerRecord -ServerFile $serverMd
        }
    }
}

$report = @()

foreach ($server in $servers) {
    $ip = $server.IP
    $name = $server.Name
    $keyPath = $server.KeyPath
    $username = $server.Username
    
    Write-Host "`n检查服务器：$name ($ip)..." -ForegroundColor White
    
    # 检查连接状态
    $status = 'OK'
    $cpu = '-'
    $memory = '-'
    $disk = '-'
    
    if (Test-Path $keyPath) {
        Write-Host "  [OK] 密钥文件存在" -ForegroundColor Green
        try {
            $probe = & ssh -i $keyPath -o BatchMode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=15 -p $server.Port "$username@$ip" "printf 'cpu=NA\nmemory=NA\ndisk=NA\n'" 2>$null
            $status = 'OK'
            foreach ($line in $probe) {
                if ($line -match '^cpu=(.+)$') { $cpu = $matches[1] }
                if ($line -match '^memory=(.+)$') { $memory = $matches[1] }
                if ($line -match '^disk=(.+)$') { $disk = $matches[1] }
            }
        } catch {
            $status = 'FAIL'
            $cpu = '连接失败'
            $memory = '连接失败'
            $disk = '连接失败'
        }
    } else {
        $status = 'FAIL'
        Write-Host "  [WARN] 密钥文件不存在" -ForegroundColor Yellow
        $cpu = '未配置密钥'
        $memory = '未配置密钥'
        $disk = '未配置密钥'
    }
    
    $report += [PSCustomObject]@{
        Name = $name
        IP = $ip
        Status = $status
        CPU = $cpu
        Memory = $memory
        Disk = $disk
    }
}

$dateToken = Get-Date -Format 'yyyyMMdd'
$reportPath = Join-Path $reportsRoot "$dateToken.md"
$generatedAt = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$markdown = @()
$markdown += '# 服务器监控报表'
$markdown += ''
$markdown += "生成时间：$generatedAt"
$markdown += "监控服务器数量：$($report.Count)"
$markdown += ''
$markdown += '| 服务器 | IP | 状态 | CPU | 内存 | 硬盘 |'
$markdown += '|--------|----|------|-----|------|------|'
foreach ($item in $report) {
    $markdown += "| $($item.Name) | $($item.IP) | $($item.Status) | $($item.CPU) | $($item.Memory) | $($item.Disk) |"
}
$markdown += ''
$markdown += '## 异常告警'

$alerts = @($report | Where-Object { $_.Status -eq 'FAIL' })
if ($alerts.Count -eq 0) {
    $markdown += '- 无异常'
} else {
    foreach ($alert in $alerts) {
        $markdown += "- $($alert.Name) ($($alert.IP)) 连接失败或未配置密钥"
    }
}

Set-Content -Path $reportPath -Value $markdown -Encoding UTF8
Write-Host "`n报表已保存：$reportPath" -ForegroundColor Green
Write-Host ""

# 显示报表
Write-Host "| 服务器 | IP | 状态 | CPU | 内存 | 硬盘 |" -ForegroundColor White
Write-Host "|--------|----|------|-----|------|------|" -ForegroundColor White

foreach ($r in $report) {
    Write-Host "| $($r.Name) | $($r.IP) | $($r.Status) | $($r.CPU) | $($r.Memory) | $($r.Disk) |" -ForegroundColor White
}

# 异常提醒
Write-Host "`n异常提醒：" -ForegroundColor Yellow
$alerts = @($report | Where-Object { $_.Status -eq 'FAIL' })
if ($alerts.Count -gt 0) {
    foreach ($alert in $alerts) {
        Write-Host "- $($alert.Name) ($($alert.IP)) 连接失败" -ForegroundColor Red
    }
} else {
    Write-Host "无异常" -ForegroundColor Green
}