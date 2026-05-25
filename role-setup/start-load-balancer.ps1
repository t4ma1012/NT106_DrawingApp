param(
    [int]$ListenPort = 9000,
    [string]$Server1Host = '127.0.0.1',
    [int]$Server1TcpPort = 8888,
    [int]$Server1UdpPort = 8889,
    [string]$Server2Host = '127.0.0.1',
    [int]$Server2TcpPort = 8890,
    [int]$Server2UdpPort = 8891,
    [switch]$Build,
    [switch]$StartPlayitAgent,
    [switch]$StopExisting
)

. (Join-Path $PSScriptRoot '_common.ps1')

$repoRoot = Get-RepoRoot
if ($Build.IsPresent) {
    Invoke-RoleBuild -RepoRoot $repoRoot -Project 'LoadBalancer\LoadBalancer.csproj'
}

if ($StopExisting.IsPresent) {
    Stop-Ports -Ports @($ListenPort)
}

$serversPath = Write-ServersJson `
    -RepoRoot $repoRoot `
    -Server1Host $Server1Host `
    -Server1TcpPort $Server1TcpPort `
    -Server1UdpPort $Server1UdpPort `
    -Server2Host $Server2Host `
    -Server2TcpPort $Server2TcpPort `
    -Server2UdpPort $Server2UdpPort

$lbExe = Resolve-RoleExe -Name 'LoadBalancer' -Candidates @(
    (Join-Path $repoRoot 'LoadBalancer\bin\Debug\LoadBalancer.exe'),
    (Join-Path $repoRoot 'LoadBalancer\bin\Release\LoadBalancer.exe')
)

if ($StartPlayitAgent.IsPresent) {
    $playitExe = Resolve-PlayitExe
    if ($null -eq $playitExe) {
        Write-Host 'playit executable not found. Set PLAYIT_EXE or add playit to PATH.'
    }
    else {
        Start-Process powershell.exe -ArgumentList @('-NoExit', '-Command', "& '$playitExe'") | Out-Null
        Write-Host 'Started playit agent. Configure a TCP tunnel to this LoadBalancer port in the playit dashboard.'
    }
}

$env:LOAD_BALANCER_PORT = "$ListenPort"
$env:LOAD_BALANCER_STRATEGY = 'room-affinity'

Write-Host "servers.json: $serversPath"
Write-Host "Starting LoadBalancer TCP=$ListenPort"
Set-Location (Join-Path $repoRoot 'LoadBalancer')
& $lbExe
