param(
    [ValidateSet('Direct', 'LbRelay', 'LbDirect')]
    [string]$Mode = 'LbRelay',
    [string]$Host = '127.0.0.1',
    [int]$TcpPort = 9000,
    [int]$UdpPort = 8889,
    [string]$ClientLabel = 'Client',
    [switch]$Build
)

. (Join-Path $PSScriptRoot '_common.ps1')

$repoRoot = Get-RepoRoot
if ($Build.IsPresent) {
    Invoke-RoleBuild -RepoRoot $repoRoot -Project 'DrawingClient\DrawingClient.csproj'
}

$clientExe = Resolve-RoleExe -Name 'DrawingClient' -Candidates @(
    (Join-Path $repoRoot 'DrawingClient\bin\Debug\DrawingClient.exe'),
    (Join-Path $repoRoot 'DrawingClient\bin\Release\DrawingClient.exe')
)

switch ($Mode) {
    'Direct' {
        $env:USE_LOAD_BALANCER_ROUTING = '0'
        $env:LOAD_BALANCER_HOST = $Host
        $env:SERVER_TCP_PORT = "$TcpPort"
        $env:SERVER_UDP_PORT = "$UdpPort"
        Write-Host "Client direct mode: TCP=${Host}:$TcpPort UDP=${Host}:$UdpPort"
    }
    'LbDirect' {
        $env:USE_LOAD_BALANCER_ROUTING = '1'
        $env:LOAD_BALANCER_CLIENT_MODE = 'direct'
        $env:LOAD_BALANCER_HOST = $Host
        $env:LOAD_BALANCER_PORT = "$TcpPort"
        Write-Host "Client LB direct-route mode: route via LB ${Host}:$TcpPort, then connect backend TCP+UDP."
    }
    default {
        $env:USE_LOAD_BALANCER_ROUTING = '1'
        $env:LOAD_BALANCER_CLIENT_MODE = 'relay'
        $env:LOAD_BALANCER_HOST = $Host
        $env:LOAD_BALANCER_PORT = "$TcpPort"
        Write-Host "Client LB relay mode: TCP relay via ${Host}:$TcpPort; realtime cursor/laser uses TCP fallback."
    }
}

$Host.UI.RawUI.WindowTitle = $ClientLabel
Set-Location (Join-Path $repoRoot 'DrawingClient')
& $clientExe
