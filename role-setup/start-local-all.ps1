param(
    [switch]$Build,
    [switch]$StopExisting,
    [int]$ClientCount = 3
)

. (Join-Path $PSScriptRoot '_common.ps1')

$repoRoot = Get-RepoRoot
if ($Build.IsPresent) {
    dotnet build (Join-Path $repoRoot 'NT106_DrawingApp.sln') -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'Solution build failed.'
    }
}

if ($StopExisting.IsPresent) {
    Stop-Ports -Ports @(8888, 8889, 8890, 8891, 9000)
}

$server1 = Join-Path $PSScriptRoot 'start-server.ps1'
$server2 = Join-Path $PSScriptRoot 'start-server.ps1'
$lb = Join-Path $PSScriptRoot 'start-load-balancer.ps1'
$client = Join-Path $PSScriptRoot 'start-client.ps1'

Start-Process powershell.exe -ArgumentList @('-NoExit', '-ExecutionPolicy', 'Bypass', '-File', $server1, '-ServerId', 'server-1', '-TcpPort', '8888', '-UdpPort', '8889') | Out-Null
Start-Sleep -Milliseconds 500
Start-Process powershell.exe -ArgumentList @('-NoExit', '-ExecutionPolicy', 'Bypass', '-File', $server2, '-ServerId', 'server-2', '-TcpPort', '8890', '-UdpPort', '8891') | Out-Null
Start-Sleep -Milliseconds 500
Start-Process powershell.exe -ArgumentList @('-NoExit', '-ExecutionPolicy', 'Bypass', '-File', $lb, '-ListenPort', '9000', '-Server1Host', '127.0.0.1', '-Server1TcpPort', '8888', '-Server1UdpPort', '8889', '-Server2Host', '127.0.0.1', '-Server2TcpPort', '8890', '-Server2UdpPort', '8891') | Out-Null
Start-Sleep -Milliseconds 800

for ($i = 1; $i -le $ClientCount; $i++) {
    Start-Process powershell.exe -ArgumentList @('-NoExit', '-ExecutionPolicy', 'Bypass', '-File', $client, '-Mode', 'LbRelay', '-Host', '127.0.0.1', '-TcpPort', '9000', '-ClientLabel', "Client-$i") | Out-Null
}

Write-Host "Started local demo: 2 servers + 1 LoadBalancer + $ClientCount clients."
