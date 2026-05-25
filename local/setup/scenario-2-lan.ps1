param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('LoadBalancer', 'Server1', 'Server2', 'Client')]
    [string]$Role,
    [string]$RepoRoot = '',
    [string]$LbHost = '',
    [int]$LbPort = 0,
    [string]$Server1Host = '',
    [int]$Server1TcpPort = 0,
    [int]$Server1UdpPort = 0,
    [string]$Server2Host = '',
    [int]$Server2TcpPort = 0,
    [int]$Server2UdpPort = 0,
    [string]$ClientLabel = 'Client'
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

function Ask-String {
    param(
        [string]$Prompt,
        [string]$DefaultValue = ''
    )

    $suffix = if ([string]::IsNullOrWhiteSpace($DefaultValue)) { '' } else { " [$DefaultValue]" }
    $value = Read-Host "$Prompt$suffix"
    if ([string]::IsNullOrWhiteSpace($value)) { return $DefaultValue }
    return $value
}

function Ask-Int {
    param(
        [string]$Prompt,
        [int]$DefaultValue
    )

    while ($true) {
        $suffix = if ($DefaultValue -gt 0) { " [$DefaultValue]" } else { '' }
        $value = Read-Host "$Prompt$suffix"
        if ([string]::IsNullOrWhiteSpace($value)) { return $DefaultValue }
        $parsed = 0
        if ([int]::TryParse($value, [ref]$parsed)) { return $parsed }
        Write-Host 'Nhap so hop le.'
    }
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

function Write-LoadBalancerSummary {
    param(
        [string]$Label,
        [string]$PublicHost,
        [int]$Port,
        [string]$Server1Host,
        [int]$Server1TcpPort,
        [int]$Server1UdpPort,
        [string]$Server2Host,
        [int]$Server2TcpPort,
        [int]$Server2UdpPort
    )

    Write-Host "--- $Label summary ---"
    Write-Host ("LB endpoint: {0}:{1}" -f $PublicHost, $Port)
    Write-Host ("Server 1 backend: {0}:{1}/{2}" -f $Server1Host, $Server1TcpPort, $Server1UdpPort)
    Write-Host ("Server 2 backend: {0}:{1}/{2}" -f $Server2Host, $Server2TcpPort, $Server2UdpPort)
    Write-Host ("Client command: scenario-2-lan.ps1 -Role Client -LbHost {0} -LbPort {1}" -f $PublicHost, $Port)
}

function Write-ServersJson {
    param(
        [string]$Path,
        [string]$FirstHost,
        [int]$FirstTcpPort,
        [int]$FirstUdpPort,
        [string]$SecondHost,
        [int]$SecondTcpPort,
        [int]$SecondUdpPort
    )

    $servers = @(
        [pscustomobject]@{
            server_id = 'server-1'
            name      = 'DrawingServer-1'
            host      = $FirstHost
            tcp_port  = $FirstTcpPort
            udp_port  = $FirstUdpPort
        },
        [pscustomobject]@{
            server_id = 'server-2'
            name      = 'DrawingServer-2'
            host      = $SecondHost
            tcp_port  = $SecondTcpPort
            udp_port  = $SecondUdpPort
        }
    )

    $servers | ConvertTo-Json -Depth 5 | Set-Content -Path $Path -Encoding UTF8
}

$serverProject = Join-Path $RepoRoot 'DrawingServer\DrawingServer.csproj'
$loadBalancerProject = Join-Path $RepoRoot 'LoadBalancer\LoadBalancer.csproj'
$clientProject = Join-Path $RepoRoot 'DrawingClient\DrawingClient.csproj'
$loadBalancerDir = Join-Path $RepoRoot 'LoadBalancer'

switch ($Role) {
    'LoadBalancer' {
        if ([string]::IsNullOrWhiteSpace($LbHost)) { $LbHost = Ask-String 'Nhap IP/host public cua LoadBalancer hoac ngrok' '127.0.0.1' }
        if ($LbPort -le 0) { $LbPort = Ask-Int 'Nhap port public cua LoadBalancer hoac ngrok' 9000 }

        if ([string]::IsNullOrWhiteSpace($Server1Host)) { $Server1Host = Ask-String 'Nhap IP/host cua server 1 trong LAN' '127.0.0.1' }
        if ($Server1TcpPort -le 0) { $Server1TcpPort = Ask-Int 'Nhap TCP port cua server 1' 8888 }
        if ($Server1UdpPort -le 0) { $Server1UdpPort = Ask-Int 'Nhap UDP port cua server 1' 8889 }
        if ([string]::IsNullOrWhiteSpace($Server2Host)) { $Server2Host = Ask-String 'Nhap IP/host cua server 2 trong LAN' '127.0.0.1' }
        if ($Server2TcpPort -le 0) { $Server2TcpPort = Ask-Int 'Nhap TCP port cua server 2' 8890 }
        if ($Server2UdpPort -le 0) { $Server2UdpPort = Ask-Int 'Nhap UDP port cua server 2' 8891 }

        Write-ServersJson `
            -Path (Join-Path $loadBalancerDir 'servers.json') `
            -FirstHost $Server1Host `
            -FirstTcpPort $Server1TcpPort `
            -FirstUdpPort $Server1UdpPort `
            -SecondHost $Server2Host `
            -SecondTcpPort $Server2TcpPort `
            -SecondUdpPort $Server2UdpPort

        Stop-ProcessesUsingPorts -Ports @($LbPort)

        $loadBalancerExe = Resolve-ExecutablePath `
            -DisplayName 'LoadBalancer' `
            -Candidates @(
                (Join-Path $RepoRoot 'LoadBalancer\bin\Debug\LoadBalancer.exe'),
                (Join-Path $RepoRoot 'LoadBalancer\bin\Release\LoadBalancer.exe')
            )

        Invoke-RoleWindow `
            -Title 'LoadBalancer-LAN' `
            -WorkingDir $loadBalancerDir `
        -EnvLines @(
            "`$env:LOAD_BALANCER_PORT = '$LbPort'",
            "`$env:LOAD_BALANCER_STRATEGY = 'room-affinity'"
        ) `
            -ExecutablePath $loadBalancerExe

        Write-LoadBalancerSummary -Label 'LAN' -PublicHost $LbHost -Port $LbPort -Server1Host $Server1Host -Server1TcpPort $Server1TcpPort -Server1UdpPort $Server1UdpPort -Server2Host $Server2Host -Server2TcpPort $Server2TcpPort -Server2UdpPort $Server2UdpPort
    }
    'Server1' {
        if ([string]::IsNullOrWhiteSpace($Server1Host)) { $Server1Host = Ask-String 'Nhap IP/host server 1 (neu chay chung may co the de 127.0.0.1)' '127.0.0.1' }
        if ($Server1TcpPort -le 0) { $Server1TcpPort = Ask-Int 'Nhap TCP port server 1' 8888 }
        if ($Server1UdpPort -le 0) { $Server1UdpPort = Ask-Int 'Nhap UDP port server 1' 8889 }

        $serverExe = Resolve-ExecutablePath `
            -DisplayName 'DrawingServer' `
            -Candidates @(
                (Join-Path $RepoRoot 'DrawingServer\bin\Debug\net472\DrawingServer.exe'),
                (Join-Path $RepoRoot 'DrawingServer\bin\Release\net472\DrawingServer.exe'),
                (Join-Path $RepoRoot 'DrawingServer\bin\Debug\DrawingServer.exe'),
                (Join-Path $RepoRoot 'DrawingServer\bin\Release\DrawingServer.exe')
            )

        Invoke-RoleWindow `
            -Title 'DrawingServer-1-LAN' `
            -WorkingDir (Join-Path $RepoRoot 'DrawingServer') `
            -EnvLines @(
                "`$env:SERVER_ID = 'server-1'",
                "`$env:SERVER_TCP_PORT = '$Server1TcpPort'",
                "`$env:SERVER_UDP_PORT = '$Server1UdpPort'",
                "`$env:SERVER_LOG_FILE = 'server_logs_server-1.txt'"
            ) `
            -ExecutablePath $serverExe
    }
    'Server2' {
        if ([string]::IsNullOrWhiteSpace($Server2Host)) { $Server2Host = Ask-String 'Nhap IP/host server 2 (neu chay chung may co the de 127.0.0.1)' '127.0.0.1' }
        if ($Server2TcpPort -le 0) { $Server2TcpPort = Ask-Int 'Nhap TCP port server 2' 8890 }
        if ($Server2UdpPort -le 0) { $Server2UdpPort = Ask-Int 'Nhap UDP port server 2' 8891 }

        $serverExe = Resolve-ExecutablePath `
            -DisplayName 'DrawingServer' `
            -Candidates @(
                (Join-Path $RepoRoot 'DrawingServer\bin\Debug\net472\DrawingServer.exe'),
                (Join-Path $RepoRoot 'DrawingServer\bin\Release\net472\DrawingServer.exe'),
                (Join-Path $RepoRoot 'DrawingServer\bin\Debug\DrawingServer.exe'),
                (Join-Path $RepoRoot 'DrawingServer\bin\Release\DrawingServer.exe')
            )

        Invoke-RoleWindow `
            -Title 'DrawingServer-2-LAN' `
            -WorkingDir (Join-Path $RepoRoot 'DrawingServer') `
            -EnvLines @(
                "`$env:SERVER_ID = 'server-2'",
                "`$env:SERVER_TCP_PORT = '$Server2TcpPort'",
                "`$env:SERVER_UDP_PORT = '$Server2UdpPort'",
                "`$env:SERVER_LOG_FILE = 'server_logs_server-2.txt'"
            ) `
            -ExecutablePath $serverExe
    }
    'Client' {
        if ([string]::IsNullOrWhiteSpace($LbHost)) { $LbHost = Ask-String 'Nhap IP/host cua LoadBalancer hoac ngrok' }
        if ($LbPort -le 0) { $LbPort = Ask-Int 'Nhap port cua LoadBalancer hoac ngrok' 9000 }

        $clientExe = Resolve-ExecutablePath `
            -DisplayName 'DrawingClient' `
            -Candidates @(
                (Join-Path $RepoRoot 'DrawingClient\bin\Debug\DrawingClient.exe'),
                (Join-Path $RepoRoot 'DrawingClient\bin\Release\DrawingClient.exe')
            )

        Invoke-RoleWindow `
            -Title $ClientLabel `
            -WorkingDir (Join-Path $RepoRoot 'DrawingClient') `
            -EnvLines @(
                "`$env:USE_LOAD_BALANCER_ROUTING = '1'",
                "`$env:LOAD_BALANCER_CLIENT_MODE = 'relay'",
                "`$env:LOAD_BALANCER_HOST = '$LbHost'",
                "`$env:LOAD_BALANCER_PORT = '$LbPort'"
            ) `
            -ExecutablePath $clientExe
    }
}

Write-Host "LAN scenario role started: $Role"
Write-Host 'If this is the LoadBalancer machine, make sure the server hosts/ports passed above match the actual LAN addresses.'
