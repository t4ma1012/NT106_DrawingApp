param(
    [int]$TcpPort = 0,
    [int]$UdpPort = 0,
    [string]$Server1Host = "",
    [int]$Server1TcpPort = 0,
    [int]$Server1UdpPort = 0,
    [string]$Server2Host = "",
    [int]$Server2TcpPort = 0,
    [int]$Server2UdpPort = 0,
    [switch]$StartNgrok,
    [switch]$StopExisting
)

. (Join-Path $PSScriptRoot '_common.ps1')
Load-SetupEnv | Out-Null

$setupRoot = Get-SetupRoot
$lbDir = Join-Path $setupRoot 'apps\LoadBalancer'
$lbExe = Resolve-SetupExe -Name 'LoadBalancer' -Candidates @(
    (Join-Path $lbDir 'LoadBalancer.exe')
)

if ($TcpPort -le 0) { $TcpPort = Get-SetupEnvInt -Key 'LOAD_BALANCER_PORT' -Default $script:DefaultLbTcp }
if ($UdpPort -le 0) { $UdpPort = Get-SetupEnvInt -Key 'LOAD_BALANCER_UDP_PORT' -Default $script:DefaultLbUdp }
if ($Server1TcpPort -le 0) { $Server1TcpPort = Get-SetupEnvInt -Key 'LB_SERVER_1_TCP_PORT' -Default $script:DefaultServer1Tcp }
if ($Server1UdpPort -le 0) { $Server1UdpPort = Get-SetupEnvInt -Key 'LB_SERVER_1_UDP_PORT' -Default $script:DefaultServer1Udp }
if ($Server2TcpPort -le 0) { $Server2TcpPort = Get-SetupEnvInt -Key 'LB_SERVER_2_TCP_PORT' -Default $script:DefaultServer2Tcp }
if ($Server2UdpPort -le 0) { $Server2UdpPort = Get-SetupEnvInt -Key 'LB_SERVER_2_UDP_PORT' -Default $script:DefaultServer2Udp }

if ([string]::IsNullOrWhiteSpace($Server1Host)) {
    $Server1Host = Get-SetupEnv -Key 'LB_SERVER_1_HOST' -Default ''
}
if ([string]::IsNullOrWhiteSpace($Server1Host)) {
    $Server1Host = Read-SetupHost `
        -Label 'Server-1 host/IP' `
        -Default '127.0.0.1' `
        -Hint 'Nhap IP may dang chay server-1. Lay IP LAN bang ipconfig, hoac IP Tailscale bang "tailscale ip -4". Mac dinh 127.0.0.1 nghia la server-1 chay cung may voi LoadBalancer.'
}

if ([string]::IsNullOrWhiteSpace($Server2Host)) {
    $Server2Host = Get-SetupEnv -Key 'LB_SERVER_2_HOST' -Default ''
}
if ([string]::IsNullOrWhiteSpace($Server2Host)) {
    $Server2Host = Read-SetupHost `
        -Label 'Server-2 host/IP' `
        -Default $Server1Host `
        -Hint 'Nhap IP may dang chay server-2. Mac dinh bang server-1 nghia la hai server chay tren cung mot may, dung port mac dinh khac nhau.'
}

$DatabaseUrl = Get-SetupEnv -Key 'DATABASE_URL' -Required

Ensure-SetupPortsAvailable -Ports @($TcpPort, $UdpPort) -StopExisting:$StopExisting.IsPresent

$serversPath = Write-SetupServersJson `
    -LoadBalancerDir $lbDir `
    -Server1Host $Server1Host `
    -Server1TcpPort $Server1TcpPort `
    -Server1UdpPort $Server1UdpPort `
    -Server2Host $Server2Host `
    -Server2TcpPort $Server2TcpPort `
    -Server2UdpPort $Server2UdpPort

$env:LOAD_BALANCER_PORT = "$TcpPort"
$env:LOAD_BALANCER_UDP_PORT = "$UdpPort"
$env:LOAD_BALANCER_STRATEGY = 'room-affinity'
$env:DATABASE_URL = $DatabaseUrl

Write-Host "servers.json: $serversPath"
Write-Host "Backend: server-1=${Server1Host}:$Server1TcpPort, server-2=${Server2Host}:$Server2TcpPort"
Write-Host "Starting LoadBalancer | TCP=$TcpPort UDP=$UdpPort"
Set-Location $lbDir
if ($StartNgrok.IsPresent) {
    $script = Join-Path ([System.IO.Path]::GetTempPath()) ("nt106-lb-" + [Guid]::NewGuid().ToString() + ".ps1")
    $lines = @(
        "`$Host.UI.RawUI.WindowTitle = 'load-balancer'",
        "Set-Location '$lbDir'",
        "`$env:LOAD_BALANCER_PORT = '$TcpPort'",
        "`$env:LOAD_BALANCER_UDP_PORT = '$UdpPort'",
        "`$env:LOAD_BALANCER_STRATEGY = 'room-affinity'",
        "`$env:DATABASE_URL = '$($DatabaseUrl -replace '''', '''''')'",
        "& '$lbExe'"
    )
    Set-Content -Path $script -Value ($lines -join "`r`n") -Encoding UTF8
    Start-Process powershell.exe -ArgumentList @('-NoExit', '-ExecutionPolicy', 'Bypass', '-File', $script) | Out-Null
    Wait-SetupTcpPort -HostName '127.0.0.1' -Port $TcpPort -TimeoutSeconds 25 -Name 'load-balancer'
    Start-SetupNgrokTcp -LocalPort $TcpPort | Out-Null
    Write-Host 'Ngrok chi public TCP. Client internet phai dung TCP fallback, khong bat UDP proxy.'
} else {
    & $lbExe
}
