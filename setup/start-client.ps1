param(
    [ValidateSet('Direct', 'LbRelay', 'LbDirect')]
    [string]$Mode = "",
    [Alias('Host')]
    [string]$TargetHost = "",
    [int]$TcpPort = 0,
    [int]$UdpPort = 0,
    [switch]$InternetNgrok,
    [switch]$EnableLbUdpProxy
)

. (Join-Path $PSScriptRoot '_common.ps1')
Load-SetupEnv | Out-Null

$setupRoot = Get-SetupRoot
$clientDir = Join-Path $setupRoot 'apps\DrawingClient'
$clientExe = Resolve-SetupExe -Name 'DrawingClient' -Candidates @(
    (Join-Path $clientDir 'DrawingClient.exe')
)

if ([string]::IsNullOrWhiteSpace($Mode)) {
    $useLoadBalancer = Get-SetupEnv -Key 'USE_LOAD_BALANCER_ROUTING' -Default '1'
    $lbMode = (Get-SetupEnv -Key 'LOAD_BALANCER_CLIENT_MODE' -Default 'relay').Trim().ToLowerInvariant()
    if ($useLoadBalancer -eq '0') {
        $Mode = 'Direct'
    } elseif ($lbMode -eq 'direct') {
        $Mode = 'LbDirect'
    } else {
        $Mode = 'LbRelay'
    }
}

$useInternetNgrok = $InternetNgrok.IsPresent
if (-not $useInternetNgrok -and $Mode -eq 'LbRelay') {
    $envHost = Get-SetupEnv -Key 'LOAD_BALANCER_HOST' -Default '127.0.0.1'
    if ($envHost -match '\.ngrok\.io$' -or $envHost -match '\.ngrok-free\.app$') {
        $useInternetNgrok = $true
    }
}

if ([string]::IsNullOrWhiteSpace($TargetHost)) {
    if ($Mode -eq 'Direct') {
        $TargetHost = Get-SetupEnv -Key 'SERVER_PUBLIC_HOST' -Default '127.0.0.1'
    } else {
        $TargetHost = Get-SetupEnv -Key 'LOAD_BALANCER_HOST' -Default '127.0.0.1'
    }
}

if ($useInternetNgrok -and ($TargetHost -eq '127.0.0.1' -or $TargetHost -eq 'localhost')) {
    $TargetHost = Read-SetupHost `
        -Label 'Ngrok host' `
        -Default $TargetHost `
        -Hint 'Nhap host trong endpoint ngrok tcp://host:port, vi du 0.tcp.ap.ngrok.io. Mac dinh 127.0.0.1 chi dung khi client chay cung may voi LoadBalancer, khong phai Internet.'
}

if ($TcpPort -le 0) {
    if ($Mode -eq 'Direct') {
        $TcpPort = Get-SetupEnvInt -Key 'SERVER_TCP_PORT' -Default $script:DefaultServer1Tcp
    } else {
        $TcpPort = Get-SetupEnvInt -Key 'LOAD_BALANCER_PORT' -Default $script:DefaultLbTcp
    }
}

if ($UdpPort -le 0) {
    if ($Mode -eq 'Direct') {
        $UdpPort = Get-SetupEnvInt -Key 'SERVER_UDP_PORT' -Default $script:DefaultServer1Udp
    } else {
        $UdpPort = Get-SetupEnvInt -Key 'LOAD_BALANCER_UDP_PORT' -Default $script:DefaultLbUdp
    }
}

switch ($Mode) {
    'Direct' {
        $env:USE_LOAD_BALANCER_ROUTING = '0'
        $env:SERVER_PUBLIC_HOST = $TargetHost
        $env:LOAD_BALANCER_HOST = $TargetHost
        $env:SERVER_TCP_PORT = "$TcpPort"
        $env:SERVER_UDP_PORT = "$UdpPort"
        $env:LOAD_BALANCER_UDP_PROXY = '0'
        if ($useInternetNgrok) {
            $env:CLIENT_FORCE_TCP_REALTIME = '1'
            Write-Host "Client Direct ngrok | TCP=${TargetHost}:$TcpPort | realtime tam thoi dung TCP fallback"
        } else {
            $env:CLIENT_FORCE_TCP_REALTIME = Get-SetupEnv -Key 'CLIENT_FORCE_TCP_REALTIME' -Default '0'
            Write-Host "Client Direct | TCP=${TargetHost}:$TcpPort UDP=${TargetHost}:$UdpPort"
        }
    }
    'LbDirect' {
        $env:USE_LOAD_BALANCER_ROUTING = '1'
        $env:LOAD_BALANCER_CLIENT_MODE = 'direct'
        $env:LOAD_BALANCER_HOST = $TargetHost
        $env:LOAD_BALANCER_PORT = "$TcpPort"
        $env:LOAD_BALANCER_UDP_PORT = "$UdpPort"
        $env:LOAD_BALANCER_UDP_PROXY = '0'
        Write-Host "Client LbDirect | ROUTE ${TargetHost}:$TcpPort, sau do dung backend TCP+UDP"
    }
    default {
        $env:USE_LOAD_BALANCER_ROUTING = '1'
        $env:LOAD_BALANCER_CLIENT_MODE = 'relay'
        $env:LOAD_BALANCER_HOST = $TargetHost
        $env:LOAD_BALANCER_PORT = "$TcpPort"
        $env:LOAD_BALANCER_UDP_PORT = "$UdpPort"
        if ($useInternetNgrok) {
            $env:LOAD_BALANCER_UDP_PROXY = '0'
            Write-Host "Client LbRelay ngrok | TCP=${TargetHost}:$TcpPort | realtime tam thoi dung TCP fallback"
        } else {
            $env:LOAD_BALANCER_UDP_PROXY = if ($EnableLbUdpProxy.IsPresent) { '1' } else { Get-SetupEnv -Key 'LOAD_BALANCER_UDP_PROXY' -Default '0' }
            Write-Host "Client LbRelay | TCP=${TargetHost}:$TcpPort UDP=${TargetHost}:$UdpPort UDP_PROXY=$env:LOAD_BALANCER_UDP_PROXY"
        }
    }
}

Set-Location $clientDir
& $clientExe
