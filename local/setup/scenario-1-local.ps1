param(
    [ValidateSet('All', 'Server1', 'Server2', 'LoadBalancer', 'Client')]
    [string]$Role = 'All',
    [string]$RepoRoot = '',
    [string]$LbHost = '127.0.0.1',
    [int]$LbPort = 9000,
    [string]$ClientLabel = 'Client',
    [int]$Server1TcpPort = 8888,
    [int]$Server1UdpPort = 8889,
    [int]$Server2TcpPort = 8890,
    [int]$Server2UdpPort = 8891
)

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Start-Window {
    param(
        [string]$Title,
        [string]$WorkingDir,
        [string]$CommandBody
    )

    $tempRoot = Join-Path $env:TEMP 'NT106_DrawingApp\setup'
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

    $scriptPath = Join-Path $tempRoot ([Guid]::NewGuid().ToString() + '.ps1')
    $scriptBody = @"
`$Host.UI.RawUI.WindowTitle = '$Title'
Set-Location '$WorkingDir'
$CommandBody
"@

    Set-Content -Path $scriptPath -Value $scriptBody -Encoding UTF8
    Start-Process powershell.exe -ArgumentList @('-NoExit', '-ExecutionPolicy', 'Bypass', '-File', $scriptPath) | Out-Null
}

function Resolve-ExecutablePath {
    param(
        [string]$DisplayName,
        [string[]]$Candidates
    )

    foreach ($candidate in $Candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw "$DisplayName khong tim thay file exe. Hay build truoc roi chay lai script."
}

function Stop-ProcessesUsingPorts {
    param([int[]]$Ports)

    foreach ($port in ($Ports | Where-Object { $_ -gt 0 } | Sort-Object -Unique)) {
        try {
            $tcpOwners = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
            $udpOwners = @(Get-NetUDPEndpoint -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)

            foreach ($processId in (($tcpOwners + $udpOwners) | Where-Object { $_ -gt 0 } | Sort-Object -Unique)) {
                Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            }
        }
        catch {
            Write-Host ("Khong the don process dang giu port {0}: {1}" -f $port, $_.Exception.Message)
        }
    }
}

function Resolve-NgrokPath {
    foreach ($candidate in @($env:NGROK_PATH, $env:NGROK_EXE)) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path $candidate)) {
            return (Resolve-Path $candidate).Path
        }
    }

    $wingetPackageRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    if (Test-Path $wingetPackageRoot) {
        $wingetCandidate = Get-ChildItem -Path $wingetPackageRoot -Filter 'ngrok.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $wingetCandidate) {
            return $wingetCandidate.FullName
        }
    }

    $ngrokCommand = Get-Command ngrok -ErrorAction SilentlyContinue
    if ($null -ne $ngrokCommand) {
        if ($ngrokCommand.Path) { return $ngrokCommand.Path }
        if ($ngrokCommand.Source -and (Test-Path $ngrokCommand.Source)) { return (Resolve-Path $ngrokCommand.Source).Path }
    }

    return $null
}

function Invoke-RoleWindow {
    param(
        [string]$Title,
        [string]$WorkingDir,
        [string[]]$EnvLines,
        [string]$ExecutablePath
    )

    $bodyLines = @()
    $bodyLines += $EnvLines
    $bodyLines += "& '$ExecutablePath'"

    Start-Window -Title $Title -WorkingDir $WorkingDir -CommandBody ($bodyLines -join "`r`n")
}

function Start-NgrokWindow {
    param([int]$Port)

    $ngrokPath = Resolve-NgrokPath
    if ($null -eq $ngrokPath) {
        Write-Host 'ngrok khong co trong PATH va chua dat NGROK_PATH/NGROK_EXE; bo qua cua so ngrok.'
        return
    }

    Start-Window -Title 'ngrok' -WorkingDir $RepoRoot -CommandBody "& '$ngrokPath' tcp $Port"
}

function Write-ServersJson {
    param([string]$Path)

    $servers = @(
        [pscustomobject]@{
            server_id = 'server-1'
            name      = 'DrawingServer-1'
            host      = '127.0.0.1'
            tcp_port  = $Server1TcpPort
            udp_port  = $Server1UdpPort
        },
        [pscustomobject]@{
            server_id = 'server-2'
            name      = 'DrawingServer-2'
            host      = '127.0.0.1'
            tcp_port  = $Server2TcpPort
            udp_port  = $Server2UdpPort
        }
    )

    $servers | ConvertTo-Json -Depth 5 | Set-Content -Path $Path -Encoding UTF8
}

$serverProject = Join-Path $RepoRoot 'DrawingServer\DrawingServer.csproj'
$loadBalancerProject = Join-Path $RepoRoot 'LoadBalancer\LoadBalancer.csproj'
$clientProject = Join-Path $RepoRoot 'DrawingClient\DrawingClient.csproj'
$loadBalancerDir = Join-Path $RepoRoot 'LoadBalancer'

function Start-ClientWindow {
    param([string]$Title)

    $clientExe = Resolve-ExecutablePath `
        -DisplayName 'DrawingClient' `
        -Candidates @(
            (Join-Path $RepoRoot 'DrawingClient\bin\Debug\DrawingClient.exe'),
            (Join-Path $RepoRoot 'DrawingClient\bin\Release\DrawingClient.exe')
        )

    Invoke-RoleWindow `
        -Title $Title `
        -WorkingDir (Join-Path $RepoRoot 'DrawingClient') `
        -EnvLines @(
            "`$env:USE_LOAD_BALANCER_ROUTING = '1'",
            "`$env:LOAD_BALANCER_CLIENT_MODE = 'relay'",
            "`$env:LOAD_BALANCER_HOST = '$LbHost'",
            "`$env:LOAD_BALANCER_PORT = '$LbPort'"
        ) `
        -ExecutablePath $clientExe
}

function Start-Server1Window {
    Stop-ProcessesUsingPorts -Ports @($Server1TcpPort, $Server1UdpPort)

    $serverExe = Resolve-ExecutablePath `
        -DisplayName 'DrawingServer' `
        -Candidates @(
            (Join-Path $RepoRoot 'DrawingServer\bin\Debug\net472\DrawingServer.exe'),
            (Join-Path $RepoRoot 'DrawingServer\bin\Release\net472\DrawingServer.exe'),
            (Join-Path $RepoRoot 'DrawingServer\bin\Debug\DrawingServer.exe'),
            (Join-Path $RepoRoot 'DrawingServer\bin\Release\DrawingServer.exe')
        )

    Invoke-RoleWindow `
        -Title 'DrawingServer-1' `
        -WorkingDir (Join-Path $RepoRoot 'DrawingServer') `
        -EnvLines @(
            "`$env:SERVER_ID = 'server-1'",
            "`$env:SERVER_TCP_PORT = '$Server1TcpPort'",
            "`$env:SERVER_UDP_PORT = '$Server1UdpPort'",
            "`$env:SERVER_LOG_FILE = 'server_logs_server-1.txt'"
        ) `
        -ExecutablePath $serverExe
}

function Start-Server2Window {
    Stop-ProcessesUsingPorts -Ports @($Server2TcpPort, $Server2UdpPort)

    $serverExe = Resolve-ExecutablePath `
        -DisplayName 'DrawingServer' `
        -Candidates @(
            (Join-Path $RepoRoot 'DrawingServer\bin\Debug\net472\DrawingServer.exe'),
            (Join-Path $RepoRoot 'DrawingServer\bin\Release\net472\DrawingServer.exe'),
            (Join-Path $RepoRoot 'DrawingServer\bin\Debug\DrawingServer.exe'),
            (Join-Path $RepoRoot 'DrawingServer\bin\Release\DrawingServer.exe')
        )

    Invoke-RoleWindow `
        -Title 'DrawingServer-2' `
        -WorkingDir (Join-Path $RepoRoot 'DrawingServer') `
        -EnvLines @(
            "`$env:SERVER_ID = 'server-2'",
            "`$env:SERVER_TCP_PORT = '$Server2TcpPort'",
            "`$env:SERVER_UDP_PORT = '$Server2UdpPort'",
            "`$env:SERVER_LOG_FILE = 'server_logs_server-2.txt'"
        ) `
        -ExecutablePath $serverExe
}

function Start-LoadBalancerWindow {
    Stop-ProcessesUsingPorts -Ports @($LbPort)

    $loadBalancerExe = Resolve-ExecutablePath `
        -DisplayName 'LoadBalancer' `
        -Candidates @(
            (Join-Path $RepoRoot 'LoadBalancer\bin\Debug\LoadBalancer.exe'),
            (Join-Path $RepoRoot 'LoadBalancer\bin\Release\LoadBalancer.exe')
        )

    Write-ServersJson -Path (Join-Path $loadBalancerDir 'servers.json')

    Invoke-RoleWindow `
        -Title 'LoadBalancer' `
        -WorkingDir $loadBalancerDir `
        -EnvLines @(
            "`$env:LOAD_BALANCER_PORT = '$LbPort'",
            "`$env:LOAD_BALANCER_STRATEGY = 'room-affinity'"
        ) `
        -ExecutablePath $loadBalancerExe
}

function Write-ScenarioSummary {
    Write-Host '--- Scenario 1 summary ---'
    Write-Host ("LoadBalancer: {0}:{1}" -f $LbHost, $LbPort)
    Write-Host ("Server 1: TCP {0}, UDP {1}" -f $Server1TcpPort, $Server1UdpPort)
    Write-Host ("Server 2: TCP {0}, UDP {1}" -f $Server2TcpPort, $Server2UdpPort)
    Write-Host 'Client env: USE_LOAD_BALANCER_ROUTING=1, LOAD_BALANCER_CLIENT_MODE=relay'
}

switch ($Role) {
    'All' {
        Start-Server1Window
        Start-Server2Window
        Start-LoadBalancerWindow
        Start-ClientWindow -Title 'Client-1'
        Start-ClientWindow -Title 'Client-2'
        Start-ClientWindow -Title 'Client-3'
        Write-ScenarioSummary
        Write-Host 'Scenario 1 started: 2 servers + 1 LB + 3 clients on the same machine.'
    }
    'Server1' { Start-Server1Window }
    'Server2' { Start-Server2Window }
    'LoadBalancer' { Start-LoadBalancerWindow; Write-ScenarioSummary }
    'Client' {
        Write-Host "Client role needs LB host and port. Current defaults: host=$LbHost port=$LbPort"
        Start-ClientWindow -Title $ClientLabel
    }
}
