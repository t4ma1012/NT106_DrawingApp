param(
    [ValidateSet('server-1', 'server-2')]
    [string]$ServerId = "",
    [int]$TcpPort = 0,
    [int]$UdpPort = 0,
    [string]$PublicHost = "",
    [switch]$StartNgrok,
    [switch]$StopExisting
)

. (Join-Path $PSScriptRoot '_common.ps1')
Load-SetupEnv | Out-Null

$setupRoot = Get-SetupRoot
$serverDir = Join-Path $setupRoot 'apps\DrawingServer'
$serverExe = Resolve-SetupExe -Name 'DrawingServer' -Candidates @(
    (Join-Path $serverDir 'DrawingServer.exe'),
    (Join-Path $serverDir 'net472\DrawingServer.exe')
)

$envServerId = Get-SetupEnv -Key 'SERVER_ID' -Default 'server-1'
if ([string]::IsNullOrWhiteSpace($ServerId)) {
    $ServerId = $envServerId
}
if ($TcpPort -le 0) {
    $defaultTcp = if ($ServerId -eq 'server-2') { $script:DefaultServer2Tcp } else { $script:DefaultServer1Tcp }
    $TcpPort = if ($envServerId -eq $ServerId) { Get-SetupEnvInt -Key 'SERVER_TCP_PORT' -Default $defaultTcp } else { $defaultTcp }
}
if ($UdpPort -le 0) {
    $defaultUdp = if ($ServerId -eq 'server-2') { $script:DefaultServer2Udp } else { $script:DefaultServer1Udp }
    $UdpPort = if ($envServerId -eq $ServerId) { Get-SetupEnvInt -Key 'SERVER_UDP_PORT' -Default $defaultUdp } else { $defaultUdp }
}
if ([string]::IsNullOrWhiteSpace($PublicHost)) {
    $PublicHost = Get-SetupEnv -Key 'SERVER_PUBLIC_HOST' -Default '127.0.0.1'
}

$DatabaseUrl = Get-SetupEnv -Key 'DATABASE_URL' -Required
$configuredCert = Get-SetupEnv -Key 'SERVER_CERT_PATH' -Default 'server.pfx'
$CertPath = if ([System.IO.Path]::IsPathRooted($configuredCert)) { $configuredCert } else { Join-Path $serverDir $configuredCert }
$CertPassword = Get-SetupEnv -Key 'SERVER_CERT_PASSWORD' -Required

Ensure-SetupPortsAvailable -Ports @($TcpPort, $UdpPort) -StopExisting:$StopExisting.IsPresent
Test-SetupCertificate -CertPath $CertPath -CertPassword $CertPassword

$env:SERVER_ID = $ServerId
$env:SERVER_NAME = "DrawingServer-$($ServerId.Split('-')[-1])"
$env:SERVER_TCP_PORT = "$TcpPort"
$env:SERVER_UDP_PORT = "$UdpPort"
$env:SERVER_PUBLIC_HOST = $PublicHost
$env:DATABASE_URL = $DatabaseUrl
$env:SERVER_CERT_PATH = $CertPath
$env:SERVER_CERT_PASSWORD = $CertPassword
$env:SERVER_LOG_FILE = "server_logs_$ServerId.txt"

Write-Host "Starting $ServerId | TCP=$TcpPort UDP=$UdpPort Host=$PublicHost"
Set-Location $serverDir
if ($StartNgrok.IsPresent) {
    $envVars = @{
        DATABASE_URL = $DatabaseUrl
        SERVER_CERT_PATH = $CertPath
        SERVER_CERT_PASSWORD = $CertPassword
        SERVER_ID = $ServerId
        SERVER_NAME = $env:SERVER_NAME
        SERVER_TCP_PORT = "$TcpPort"
        SERVER_UDP_PORT = "$UdpPort"
        SERVER_PUBLIC_HOST = $PublicHost
        SERVER_LOG_FILE = $env:SERVER_LOG_FILE
    }
    Start-SetupExeWindow -Title $ServerId -WorkingDir $serverDir -ExecutablePath $serverExe -EnvVars $envVars
    Wait-SetupTcpPort -HostName '127.0.0.1' -Port $TcpPort -TimeoutSeconds 25 -Name $ServerId
    Start-SetupNgrokTcp -LocalPort $TcpPort | Out-Null
    Write-Host 'Ngrok chi public TCP. Client internet direct phai dung -Mode Direct -InternetNgrok de bat TCP fallback, khong dung UDP.'
} else {
    & $serverExe
}
