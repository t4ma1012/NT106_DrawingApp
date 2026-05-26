Set-StrictMode -Version Latest

$script:DefaultServer1Tcp = 8888
$script:DefaultServer1Udp = 8889
$script:DefaultServer2Tcp = 8890
$script:DefaultServer2Udp = 8891
$script:DefaultLbTcp = 9000
$script:DefaultLbUdp = 9001

function Get-SetupRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '.')).Path
}

function Get-SetupEnvPath {
    return (Join-Path (Get-SetupRoot) '.env')
}

function Ensure-SetupEnvFile {
    $envPath = Get-SetupEnvPath
    if (Test-Path $envPath) {
        return $envPath
    }

    $example = Join-Path (Get-SetupRoot) '.env.example'
    if (Test-Path $example) {
        Copy-Item -Force $example $envPath
    }

    throw "Chua co setup\\.env. Da tao tu .env.example neu co. Hay dien cac gia tri trong file setup\\.env, roi chay lai script."
}

function Load-SetupEnv {
    $envPath = Ensure-SetupEnvFile
    foreach ($rawLine in Get-Content $envPath) {
        if ([string]::IsNullOrWhiteSpace($rawLine)) { continue }
        $line = $rawLine.Trim()
        if ($line.StartsWith('#')) { continue }
        $sep = $line.IndexOf('=')
        if ($sep -le 0) { continue }

        $key = $line.Substring(0, $sep).Trim()
        $value = $line.Substring($sep + 1).Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        [Environment]::SetEnvironmentVariable($key, $value, [EnvironmentVariableTarget]::Process)
    }
    return $envPath
}

function Get-SetupEnv {
    param(
        [string]$Key,
        [string]$Default = "",
        [switch]$Required
    )

    $value = [Environment]::GetEnvironmentVariable($Key, [EnvironmentVariableTarget]::Process)
    if ([string]::IsNullOrWhiteSpace($value)) {
        $value = $Default
    }
    if ($Required.IsPresent -and [string]::IsNullOrWhiteSpace($value)) {
        throw "Thieu gia tri $Key trong setup\\.env"
    }
    return $value
}

function Get-SetupEnvInt {
    param(
        [string]$Key,
        [int]$Default
    )

    $raw = Get-SetupEnv -Key $Key -Default "$Default"
    $value = 0
    if ([int]::TryParse($raw, [ref]$value) -and $value -gt 0) {
        return $value
    }
    return $Default
}

function Read-SetupValue {
    param(
        [string]$Label,
        [string]$Default = "",
        [switch]$Required
    )

    while ($true) {
        $suffix = if ([string]::IsNullOrWhiteSpace($Default)) { "" } else { " [$Default]" }
        $value = Read-Host "$Label$suffix"
        if ([string]::IsNullOrWhiteSpace($value)) {
            $value = $Default
        }
        if (-not $Required.IsPresent -or -not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
        Write-Host "Gia tri nay bat buoc."
    }
}

function Read-SetupInt {
    param(
        [string]$Label,
        [int]$Default
    )

    while ($true) {
        $raw = Read-SetupValue -Label $Label -Default "$Default"
        $value = 0
        if ([int]::TryParse($raw, [ref]$value) -and $value -gt 0) {
            return $value
        }
        Write-Host "Hay nhap so port hop le."
    }
}

function Read-SetupHost {
    param(
        [string]$Label,
        [string]$Default = '127.0.0.1',
        [string]$Hint = ''
    )

    if (-not [string]::IsNullOrWhiteSpace($Hint)) {
        Write-Host $Hint
    }
    return Read-SetupValue -Label $Label -Default $Default -Required
}

function Resolve-SetupExe {
    param(
        [string]$Name,
        [string[]]$Candidates
    )

    foreach ($candidate in $Candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw "Khong tim thay $Name.exe trong setup/apps. Hay chay setup/package-release.ps1 tren may build de tao goi zip."
}

function Stop-SetupPorts {
    param([int[]]$Ports)

    foreach ($port in ($Ports | Where-Object { $_ -gt 0 } | Sort-Object -Unique)) {
        $tcpOwners = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
        $udpOwners = @(Get-NetUDPEndpoint -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
        foreach ($processId in (($tcpOwners + $udpOwners) | Where-Object { $_ -gt 0 } | Sort-Object -Unique)) {
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-SetupPortOwners {
    param([int[]]$Ports)

    $items = @()
    foreach ($port in ($Ports | Where-Object { $_ -gt 0 } | Sort-Object -Unique)) {
        $tcpOwners = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
        $udpOwners = @(Get-NetUDPEndpoint -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
        foreach ($processId in (($tcpOwners + $udpOwners) | Where-Object { $_ -gt 0 } | Sort-Object -Unique)) {
            $proc = Get-Process -Id $processId -ErrorAction SilentlyContinue
            $items += [pscustomobject]@{
                Port = $port
                ProcessId = $processId
                ProcessName = if ($proc) { $proc.ProcessName } else { 'unknown' }
            }
        }
    }
    return $items
}

function Ensure-SetupPortsAvailable {
    param(
        [int[]]$Ports,
        [switch]$StopExisting
    )

    $owners = @(Get-SetupPortOwners -Ports $Ports)
    if ($owners.Count -eq 0) {
        return
    }

    Write-Host 'Cac port can dung dang bi chiem:'
    $owners | Format-Table -AutoSize | Out-String | Write-Host

    if ($StopExisting.IsPresent) {
        Stop-SetupPorts -Ports $Ports
        Start-Sleep -Milliseconds 500
        return
    }

    $answer = Read-SetupValue -Label 'Dung cac tien trinh tren de tiep tuc? (y/n)' -Default 'y'
    if ($answer.Trim().ToLowerInvariant().StartsWith('y')) {
        Stop-SetupPorts -Ports $Ports
        Start-Sleep -Milliseconds 500
        return
    }

    throw 'Port dang bi chiem. Hay dung tien trinh cu hoac chay voi -StopExisting.'
}

function Test-SetupCertificate {
    param(
        [string]$CertPath,
        [string]$CertPassword
    )

    if (-not (Test-Path $CertPath)) {
        throw "Khong tim thay certificate: $CertPath"
    }

    try {
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertPath, $CertPassword)
        Write-Host "Certificate OK: $($cert.Subject)"
    }
    catch {
        throw "Khong doc duoc server.pfx. Kiem tra SERVER_CERT_PASSWORD. Loi: $($_.Exception.Message)"
    }
}

function Wait-SetupTcpPort {
    param(
        [string]$HostName,
        [int]$Port,
        [int]$TimeoutSeconds = 20,
        [string]$Name = 'service'
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $tcp = New-Object System.Net.Sockets.TcpClient
            $task = $tcp.ConnectAsync($HostName, $Port)
            if ($task.Wait(500) -and $tcp.Connected) {
                $tcp.Close()
                Write-Host "$Name san sang tai ${HostName}:$Port"
                return
            }
            $tcp.Close()
        }
        catch { }
        Start-Sleep -Milliseconds 500
    }

    throw "$Name khong mo TCP ${HostName}:$Port sau $TimeoutSeconds giay. Kiem tra cua so/log cua service vua mo."
}

function Wait-SetupTlsPort {
    param(
        [string]$HostName,
        [int]$Port,
        [int]$TimeoutSeconds = 20,
        [string]$Name = 'service'
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $tcp = $null
        $ssl = $null
        try {
            $tcp = New-Object System.Net.Sockets.TcpClient
            $task = $tcp.ConnectAsync($HostName, $Port)
            if ($task.Wait(500) -and $tcp.Connected) {
                $certCallback = [System.Net.Security.RemoteCertificateValidationCallback]{
                    param($sender, $cert, $chain, $errors)
                    return $true
                }
                $ssl = New-Object System.Net.Security.SslStream($tcp.GetStream(), $false, $certCallback)
                $ssl.AuthenticateAsClient(
                    'DrawingServer',
                    $null,
                    [System.Security.Authentication.SslProtocols]::Tls12,
                    $false
                )
                if ($ssl.IsAuthenticated) {
                    Write-Host "$Name san sang TLS tai ${HostName}:$Port"
                    return
                }
            }
        }
        catch { }
        finally {
            if ($ssl -ne $null) { try { $ssl.Dispose() } catch { } }
            if ($tcp -ne $null) { try { $tcp.Close() } catch { } }
        }
        Start-Sleep -Milliseconds 500
    }

    throw "$Name khong mo TLS ${HostName}:$Port sau $TimeoutSeconds giay. Kiem tra cua so/log cua service vua mo."
}

function Wait-SetupNgrokEndpoint {
    param(
        [int]$LocalPort,
        [int]$TimeoutSeconds = 25
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Uri 'http://127.0.0.1:4040/api/tunnels' -TimeoutSec 2 -ErrorAction Stop
            foreach ($tunnel in @($response.tunnels)) {
                $addr = [string]$tunnel.config.addr
                $publicUrl = [string]$tunnel.public_url
                if ($publicUrl.StartsWith('tcp://') -and ($addr.EndsWith(":$LocalPort") -or $addr -eq "$LocalPort")) {
                    $uri = [Uri]$publicUrl
                    return [pscustomobject]@{
                        Host = $uri.Host
                        Port = $uri.Port
                        Url = $publicUrl
                    }
                }
            }
        }
        catch { }
        Start-Sleep -Milliseconds 700
    }

    return $null
}

function Quote-SetupArgument {
    param([string]$Value)

    if ($null -eq $Value) {
        return '""'
    }

    return '"' + ($Value -replace '"', '\"') + '"'
}

function Start-SetupWindow {
    param(
        [string]$Title,
        [string]$ScriptPath,
        [string[]]$Arguments
    )

    $parts = @('-NoExit', '-ExecutionPolicy', 'Bypass', '-File', (Quote-SetupArgument $ScriptPath))
    foreach ($arg in $Arguments) {
        $parts += (Quote-SetupArgument $arg)
    }
    Start-Process powershell.exe -ArgumentList ($parts -join ' ') | Out-Null
    Write-Host "Da mo cua so: $Title"
}

function Start-SetupExeWindow {
    param(
        [string]$Title,
        [string]$WorkingDir,
        [string]$ExecutablePath,
        [hashtable]$EnvVars
    )

    $tempRoot = Join-Path $env:TEMP 'NT106_DrawingApp\setup-runtime'
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    $scriptPath = Join-Path $tempRoot ([Guid]::NewGuid().ToString() + '.ps1')

    $lines = @()
    $lines += "`$Host.UI.RawUI.WindowTitle = $(Quote-SetupArgument $Title)"
    $lines += "Set-Location $(Quote-SetupArgument $WorkingDir)"
    foreach ($key in $EnvVars.Keys) {
        $lines += "`$env:$key = $(Quote-SetupArgument ([string]$EnvVars[$key]))"
    }
    $lines += "& $(Quote-SetupArgument $ExecutablePath)"

    Set-Content -Path $scriptPath -Value ($lines -join "`r`n") -Encoding UTF8
    Start-Process powershell.exe -ArgumentList @('-NoExit', '-ExecutionPolicy', 'Bypass', '-File', $scriptPath) | Out-Null
    Write-Host "Da mo cua so: $Title"
}

function Write-SetupServersJson {
    param(
        [string]$LoadBalancerDir,
        [string]$Server1Host,
        [int]$Server1TcpPort,
        [int]$Server1UdpPort,
        [string]$Server2Host,
        [int]$Server2TcpPort,
        [int]$Server2UdpPort
    )

    New-Item -ItemType Directory -Force -Path $LoadBalancerDir | Out-Null
    $servers = @(
        [pscustomobject]@{
            server_id = 'server-1'
            name = 'DrawingServer-1'
            host = $Server1Host
            tcp_port = $Server1TcpPort
            udp_port = $Server1UdpPort
        },
        [pscustomobject]@{
            server_id = 'server-2'
            name = 'DrawingServer-2'
            host = $Server2Host
            tcp_port = $Server2TcpPort
            udp_port = $Server2UdpPort
        }
    )

    $path = Join-Path $LoadBalancerDir 'servers.json'
    $servers | ConvertTo-Json -Depth 5 | Set-Content -Path $path -Encoding UTF8
    return $path
}

function Resolve-NgrokExe {
    foreach ($candidate in @($env:NGROK_EXE, $env:NGROK_PATH)) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path $candidate)) {
            return (Resolve-Path $candidate).Path
        }
    }

    $cmd = Get-Command ngrok -ErrorAction SilentlyContinue
    if ($null -ne $cmd) {
        if ($cmd.Path) { return $cmd.Path }
        if ($cmd.Source -and (Test-Path $cmd.Source)) { return (Resolve-Path $cmd.Source).Path }
    }

    return $null
}

function Start-SetupNgrokTcp {
    param(
        [int]$LocalPort,
        [int]$TimeoutSeconds = 25
    )

    $ngrokExe = Resolve-NgrokExe
    if ($null -eq $ngrokExe) {
        Write-Host 'Khong tim thay ngrok. Cai ngrok CLI hoac set NGROK_EXE/NGROK_PATH trong moi truong.'
        return $null
    }

    $token = Get-SetupEnv -Key 'NGROK_AUTHTOKEN' -Default ''
    if (-not [string]::IsNullOrWhiteSpace($token)) {
        & $ngrokExe config add-authtoken $token | Out-Null
    }

    Start-Process powershell.exe -ArgumentList @('-NoExit', '-ExecutionPolicy', 'Bypass', '-Command', "& '$ngrokExe' tcp $LocalPort") | Out-Null
    Write-Host "Da mo ngrok TCP tunnel toi 127.0.0.1:$LocalPort"

    $endpoint = Wait-SetupNgrokEndpoint -LocalPort $LocalPort -TimeoutSeconds $TimeoutSeconds
    if ($null -ne $endpoint) {
        Write-Host "Ngrok public endpoint: $($endpoint.Url)"
        Write-Host "Client internet dung: -Mode LbRelay -InternetNgrok -Host $($endpoint.Host) -TcpPort $($endpoint.Port)"
    } else {
        Write-Host 'Chua doc duoc endpoint tu ngrok API 127.0.0.1:4040. Xem cua so ngrok va lay host/port tcp://...'
    }

    return $endpoint
}
