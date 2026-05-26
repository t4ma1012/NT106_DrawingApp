param(
    [int]$ClientCount = 3,
    [switch]$StartNgrok,
    [switch]$StopExisting
)

. (Join-Path $PSScriptRoot '_common.ps1')
Load-SetupEnv | Out-Null

$DatabaseUrl = Get-SetupEnv -Key 'DATABASE_URL' -Required
$CertPassword = Get-SetupEnv -Key 'SERVER_CERT_PASSWORD' -Required
Ensure-SetupPortsAvailable -Ports @($script:DefaultServer1Tcp, $script:DefaultServer1Udp, $script:DefaultServer2Tcp, $script:DefaultServer2Udp, $script:DefaultLbTcp, $script:DefaultLbUdp) -StopExisting:$StopExisting.IsPresent
$serverDir = Join-Path $PSScriptRoot 'apps\DrawingServer'
$clientDir = Join-Path $PSScriptRoot 'apps\DrawingClient'
$lbDir = Join-Path $PSScriptRoot 'apps\LoadBalancer'
$serverExe = Resolve-SetupExe -Name 'DrawingServer' -Candidates @((Join-Path $serverDir 'DrawingServer.exe'))
$clientExe = Resolve-SetupExe -Name 'DrawingClient' -Candidates @((Join-Path $clientDir 'DrawingClient.exe'))
$lbExe = Resolve-SetupExe -Name 'LoadBalancer' -Candidates @((Join-Path $lbDir 'LoadBalancer.exe'))
$configuredCert = Get-SetupEnv -Key 'SERVER_CERT_PATH' -Default 'server.pfx'
$certPath = if ([System.IO.Path]::IsPathRooted($configuredCert)) { $configuredCert } else { Join-Path $serverDir $configuredCert }
Test-SetupCertificate -CertPath $certPath -CertPassword $CertPassword

Start-SetupExeWindow -Title 'server-1' -WorkingDir $serverDir -ExecutablePath $serverExe -EnvVars @{
    DATABASE_URL = $DatabaseUrl; SERVER_CERT_PATH = $certPath; SERVER_CERT_PASSWORD = $CertPassword
    SERVER_ID = 'server-1'; SERVER_NAME = 'DrawingServer-1'; SERVER_TCP_PORT = "$script:DefaultServer1Tcp"; SERVER_UDP_PORT = "$script:DefaultServer1Udp"; SERVER_PUBLIC_HOST = '127.0.0.1'; SERVER_LOG_FILE = 'server_logs_server-1.txt'
}
Wait-SetupTcpPort -HostName '127.0.0.1' -Port $script:DefaultServer1Tcp -TimeoutSeconds 25 -Name 'server-1'
Start-SetupExeWindow -Title 'server-2' -WorkingDir $serverDir -ExecutablePath $serverExe -EnvVars @{
    DATABASE_URL = $DatabaseUrl; SERVER_CERT_PATH = $certPath; SERVER_CERT_PASSWORD = $CertPassword
    SERVER_ID = 'server-2'; SERVER_NAME = 'DrawingServer-2'; SERVER_TCP_PORT = "$script:DefaultServer2Tcp"; SERVER_UDP_PORT = "$script:DefaultServer2Udp"; SERVER_PUBLIC_HOST = '127.0.0.1'; SERVER_LOG_FILE = 'server_logs_server-2.txt'
}
Wait-SetupTcpPort -HostName '127.0.0.1' -Port $script:DefaultServer2Tcp -TimeoutSeconds 25 -Name 'server-2'
$serversPath = Write-SetupServersJson -LoadBalancerDir $lbDir -Server1Host '127.0.0.1' -Server1TcpPort $script:DefaultServer1Tcp -Server1UdpPort $script:DefaultServer1Udp -Server2Host '127.0.0.1' -Server2TcpPort $script:DefaultServer2Tcp -Server2UdpPort $script:DefaultServer2Udp
Write-Host "servers.json: $serversPath"
Start-SetupExeWindow -Title 'load-balancer' -WorkingDir $lbDir -ExecutablePath $lbExe -EnvVars @{
    DATABASE_URL = $DatabaseUrl; LOAD_BALANCER_PORT = "$script:DefaultLbTcp"; LOAD_BALANCER_UDP_PORT = "$script:DefaultLbUdp"; LOAD_BALANCER_STRATEGY = 'room-affinity'
}
Wait-SetupTcpPort -HostName '127.0.0.1' -Port $script:DefaultLbTcp -TimeoutSeconds 25 -Name 'load-balancer'
if ($StartNgrok.IsPresent) {
    Start-SetupNgrokTcp -LocalPort $script:DefaultLbTcp | Out-Null
    Write-Host 'Ngrok chi public TCP. Client internet phai dung TCP fallback, khong bat UDP proxy.'
}
for ($i = 1; $i -le $ClientCount; $i++) {
    Start-SetupExeWindow -Title "client-$i" -WorkingDir $clientDir -ExecutablePath $clientExe -EnvVars @{
        USE_LOAD_BALANCER_ROUTING = '1'; LOAD_BALANCER_CLIENT_MODE = 'relay'; LOAD_BALANCER_HOST = '127.0.0.1'; LOAD_BALANCER_PORT = "$script:DefaultLbTcp"; LOAD_BALANCER_UDP_PORT = "$script:DefaultLbUdp"; LOAD_BALANCER_UDP_PROXY = '1'
    }
}
