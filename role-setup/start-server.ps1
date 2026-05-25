param(
    [ValidateSet('server-1', 'server-2')]
    [string]$ServerId = 'server-1',
    [int]$TcpPort = 8888,
    [int]$UdpPort = 8889,
    [string]$PublicHost = '127.0.0.1',
    [switch]$Build,
    [switch]$StopExisting
)

. (Join-Path $PSScriptRoot '_common.ps1')

$repoRoot = Get-RepoRoot
if ($Build.IsPresent) {
    Invoke-RoleBuild -RepoRoot $repoRoot -Project 'DrawingServer\DrawingServer.csproj'
}

if ($StopExisting.IsPresent) {
    Stop-Ports -Ports @($TcpPort, $UdpPort)
}

$serverExe = Resolve-RoleExe -Name 'DrawingServer' -Candidates @(
    (Join-Path $repoRoot 'DrawingServer\bin\Debug\net472\DrawingServer.exe'),
    (Join-Path $repoRoot 'DrawingServer\bin\Release\net472\DrawingServer.exe'),
    (Join-Path $repoRoot 'DrawingServer\bin\Debug\DrawingServer.exe'),
    (Join-Path $repoRoot 'DrawingServer\bin\Release\DrawingServer.exe')
)

$env:SERVER_ID = $ServerId
$env:SERVER_NAME = "DrawingServer-$($ServerId.Split('-')[-1])"
$env:SERVER_TCP_PORT = "$TcpPort"
$env:SERVER_UDP_PORT = "$UdpPort"
$env:SERVER_PUBLIC_HOST = $PublicHost
$env:SERVER_LOG_FILE = "server_logs_$ServerId.txt"

Write-Host "Starting $ServerId TCP=$TcpPort UDP=$UdpPort host=$PublicHost"
Set-Location (Join-Path $repoRoot 'DrawingServer')
& $serverExe
