param(
    [string]$Configuration = 'Release',
    [string]$ZipName = 'NT106-DrawingApp-setup.zip'
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$setupRoot = Resolve-Path $PSScriptRoot
$appsRoot = Join-Path $setupRoot 'apps'

dotnet restore (Join-Path $repoRoot 'NT106_DrawingApp.sln') /p:RestorePackagesConfig=true
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

dotnet build (Join-Path $repoRoot 'NT106_DrawingApp.sln') -c $Configuration -v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

Remove-Item -Recurse -Force $appsRoot -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $appsRoot | Out-Null

function Copy-AppOutput {
    param(
        [string]$ExeName,
        [string]$BuildRoot,
        [string]$Destination
    )

    $exe = Get-ChildItem -Path $BuildRoot -Recurse -Filter $ExeName | Select-Object -First 1
    if ($null -eq $exe) {
        throw "Khong tim thay $ExeName trong $BuildRoot"
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Copy-Item -Recurse -Force (Join-Path $exe.DirectoryName '*') $Destination
}

$clientOut = Join-Path $repoRoot "DrawingClient\bin\$Configuration"
$serverOut = Join-Path $repoRoot "DrawingServer\bin\$Configuration"
$lbOut = Join-Path $repoRoot "LoadBalancer\bin\$Configuration"

Copy-AppOutput -ExeName 'DrawingClient.exe' -BuildRoot $clientOut -Destination (Join-Path $appsRoot 'DrawingClient')
Copy-AppOutput -ExeName 'DrawingServer.exe' -BuildRoot $serverOut -Destination (Join-Path $appsRoot 'DrawingServer')
Copy-AppOutput -ExeName 'LoadBalancer.exe' -BuildRoot $lbOut -Destination (Join-Path $appsRoot 'LoadBalancer')

$lbServers = Join-Path $repoRoot 'LoadBalancer\servers.json'
if (Test-Path $lbServers) {
    Copy-Item -Force $lbServers (Join-Path $appsRoot 'LoadBalancer\servers.json')
}

$cert = Join-Path $repoRoot 'DrawingServer\server.pfx'
if (Test-Path $cert) {
    Copy-Item -Force $cert (Join-Path $appsRoot 'DrawingServer\server.pfx')
}

$zipPath = Join-Path $repoRoot $ZipName
$stageRoot = Join-Path $repoRoot 'local\tmp_setup_package'
$stageSetup = Join-Path $stageRoot 'setup'
Remove-Item -Recurse -Force $stageRoot -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stageSetup | Out-Null
Copy-Item -Recurse -Force (Join-Path $setupRoot '*') $stageSetup
Copy-Item -Force (Join-Path $setupRoot '.env.example') (Join-Path $stageSetup '.env.example')
Remove-Item -Force (Join-Path $stageSetup '.env') -ErrorAction SilentlyContinue
Remove-Item -Force $zipPath -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stageRoot 'setup') -DestinationPath $zipPath -Force
Write-Host "Da tao goi: $zipPath"
