Set-StrictMode -Version Latest

function Get-ServerMaintainerWorkspaceRoot {
    param([string]$ScriptRoot)

    return (Split-Path (Split-Path (Split-Path $ScriptRoot -Parent) -Parent) -Parent)
}

function Get-ServersRoot {
    param([string]$ScriptRoot)

    return (Join-Path (Get-ServerMaintainerWorkspaceRoot -ScriptRoot $ScriptRoot) 'Servers')
}

function Get-ReportsRoot {
    param([string]$ScriptRoot)

    return (Join-Path (Get-ServerMaintainerWorkspaceRoot -ScriptRoot $ScriptRoot) 'Reports')
}

function Get-ServerDirectory {
    param(
        [string]$ScriptRoot,
        [string]$ServerId
    )

    $serversRoot = Get-ServersRoot -ScriptRoot $ScriptRoot
    $directPath = Join-Path $serversRoot $ServerId
    if (Test-Path $directPath) {
        return $directPath
    }

    $directories = Get-ChildItem -Path $serversRoot -Directory -ErrorAction SilentlyContinue
    foreach ($directory in $directories) {
        $serverFile = Join-Path $directory.FullName 'server.md'
        if (-not (Test-Path $serverFile)) {
            continue
        }

        $server = Read-ServerRecord -ServerFile $serverFile
        if ($server.Name -eq $ServerId -or $server.IP -eq $ServerId) {
            return $directory.FullName
        }
    }

    return $null
}

function Read-ServerRecord {
    param([string]$ServerFile)

    $content = Get-Content $ServerFile -Raw -Encoding UTF8
    $name = ''
    $ip = ''
    $port = 22
    $username = ''
    $loginMethod = ''

    if ($content -match '(?m)^#\s+(.+)$') { $name = $matches[1].Trim() }
    if ($content -match '(?m)^-\s*IP[：:]\s*(.+)$') { $ip = $matches[1].Trim() }
    if ($content -match '(?m)^-\s*端口号[：:]\s*(\d+)$') { $port = [int]$matches[1] }
    if ($content -match '(?m)^-\s*用户名[：:]\s*(.+)$') { $username = $matches[1].Trim() }
    if ($content -match '(?m)^-\s*登录方式[：:]\s*(.+)$') { $loginMethod = $matches[1].Trim() }

    return [PSCustomObject]@{
        Name = $name
        IP = $ip
        Port = $port
        Username = $username
        LoginMethod = $loginMethod
        ServerFile = $ServerFile
        ServerDirectory = Split-Path $ServerFile -Parent
        KeyPath = Join-Path (Split-Path $ServerFile -Parent) 'id_rsa'
    }
}

function Test-ServerKey {
    param([pscustomobject]$ServerRecord)

    return (Test-Path $ServerRecord.KeyPath)
}