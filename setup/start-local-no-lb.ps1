param(
    [int]$ClientCount = 3,
    [switch]$StopExisting
)

. (Join-Path $PSScriptRoot '_common.ps1')
Load-SetupEnv | Out-Null

$DatabaseUrl = Get-SetupEnv -Key 'DATABASE_URL' -Required
$CertPassword = Get-SetupEnv -Key 'SERVER_CERT_PASSWORD' -Required
$ClientConnectTimeoutMs = Get-SetupEnv -Key 'CLIENT_CONNECT_TIMEOUT_MS' -Default '6000'
Ensure-SetupPortsAvailable -Ports @($script:DefaultServer1Tcp, $script:DefaultServer1Udp) -StopExisting:$StopExisting.IsPresent
$serverDir = Join-Path $PSScriptRoot 'apps\DrawingServer'
$clientDir = Join-Path $PSScriptRoot 'apps\DrawingClient'
$serverExe = Resolve-SetupExe -Name 'DrawingServer' -Candidates @((Join-Path $serverDir 'DrawingServer.exe'))
$clientExe = Resolve-SetupExe -Name 'DrawingClient' -Candidates @((Join-Path $clientDir 'DrawingClient.exe'))
$configuredCert = Get-SetupEnv -Key 'SERVER_CERT_PATH' -Default 'server.pfx'
$certPath = if ([System.IO.Path]::IsPathRooted($configuredCert)) { $configuredCert } else { Join-Path $serverDir $configuredCert }
Test-SetupCertificate -CertPath $certPath -CertPassword $CertPassword

$client = Join-Path $PSScriptRoot 'start-client.ps1'
Start-SetupExeWindow -Title 'server-1' -WorkingDir $serverDir -ExecutablePath $serverExe -EnvVars @{
    DATABASE_URL = $DatabaseUrl
    SERVER_CERT_PATH = $certPath
    SERVER_CERT_PASSWORD = $CertPassword
    SERVER_ID = 'server-1'
    SERVER_NAME = 'DrawingServer-1'
    SERVER_TCP_PORT = "$script:DefaultServer1Tcp"
    SERVER_UDP_PORT = "$script:DefaultServer1Udp"
    SERVER_PUBLIC_HOST = '127.0.0.1'
    SERVER_LOG_FILE = 'server_logs_server-1.txt'
}
Wait-SetupTlsPort -HostName '127.0.0.1' -Port $script:DefaultServer1Tcp -TimeoutSeconds 25 -Name 'server-1'
for ($i = 1; $i -le $ClientCount; $i++) {
    Start-SetupExeWindow -Title "client-$i" -WorkingDir $clientDir -ExecutablePath $clientExe -EnvVars @{
        USE_LOAD_BALANCER_ROUTING = '0'
        LOAD_BALANCER_CLIENT_MODE = 'direct'
        LOAD_BALANCER_HOST = '127.0.0.1'
        SERVER_PUBLIC_HOST = '127.0.0.1'
        SERVER_TCP_PORT = "$script:DefaultServer1Tcp"
        SERVER_UDP_PORT = "$script:DefaultServer1Udp"
        LOAD_BALANCER_UDP_PROXY = '0'
        CLIENT_FORCE_TCP_REALTIME = '0'
        CLIENT_CONNECT_TIMEOUT_MS = $ClientConnectTimeoutMs
    }
}
