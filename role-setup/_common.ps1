Set-StrictMode -Version Latest

function Get-RepoRoot {
    $root = Resolve-Path (Join-Path $PSScriptRoot '..')
    return $root.Path
}

function Invoke-RoleBuild {
    param(
        [string]$RepoRoot,
        [string]$Project
    )

    dotnet build (Join-Path $RepoRoot $Project) -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $Project"
    }
}

function Resolve-RoleExe {
    param(
        [string]$Name,
        [string[]]$Candidates
    )

    foreach ($candidate in $Candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw "$Name exe not found. Run with -Build or build the solution first."
}

function Start-RoleWindow {
    param(
        [string]$Title,
        [string]$WorkingDir,
        [string[]]$EnvLines,
        [string]$ExecutablePath
    )

    $tempRoot = Join-Path $env:TEMP 'NT106_DrawingApp\role-setup'
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    $scriptPath = Join-Path $tempRoot ([Guid]::NewGuid().ToString() + '.ps1')
    $scriptBody = @"
`$Host.UI.RawUI.WindowTitle = '$Title'
Set-Location '$WorkingDir'
$($EnvLines -join "`r`n")
& '$ExecutablePath'
"@
    Set-Content -Path $scriptPath -Value $scriptBody -Encoding UTF8
    Start-Process powershell.exe -ArgumentList @('-NoExit', '-ExecutionPolicy', 'Bypass', '-File', $scriptPath) | Out-Null
}

function Write-ServersJson {
    param(
        [string]$RepoRoot,
        [string]$Server1Host,
        [int]$Server1TcpPort,
        [int]$Server1UdpPort,
        [string]$Server2Host,
        [int]$Server2TcpPort,
        [int]$Server2UdpPort
    )

    $servers = @(
        [pscustomobject]@{
            server_id = 'server-1'
            name = 'DrawingServer-1'
            host = $Server1Host
            tcp_port = $Server1TcpPort
            udp_port = $Server1UdpPort
        },
        [pscustomobject]@{
            server_id = 'server-2'
            name = 'DrawingServer-2'
            host = $Server2Host
            tcp_port = $Server2TcpPort
            udp_port = $Server2UdpPort
        }
    )

    $path = Join-Path $RepoRoot 'LoadBalancer\servers.json'
    $servers | ConvertTo-Json -Depth 5 | Set-Content -Path $path -Encoding UTF8
    return $path
}

function Stop-Ports {
    param([int[]]$Ports)

    foreach ($port in ($Ports | Where-Object { $_ -gt 0 } | Sort-Object -Unique)) {
        $tcpOwners = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
        $udpOwners = @(Get-NetUDPEndpoint -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
        foreach ($processId in (($tcpOwners + $udpOwners) | Where-Object { $_ -gt 0 } | Sort-Object -Unique)) {
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        }
    }
}

function Resolve-PlayitExe {
    foreach ($candidate in @($env:PLAYIT_EXE, $env:PLAYIT_PATH)) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path $candidate)) {
            return (Resolve-Path $candidate).Path
        }
    }

    $cmd = Get-Command playit -ErrorAction SilentlyContinue
    if ($null -ne $cmd) {
        if ($cmd.Path) { return $cmd.Path }
        if ($cmd.Source -and (Test-Path $cmd.Source)) { return (Resolve-Path $cmd.Source).Path }
    }

    return $null
}
