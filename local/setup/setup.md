# Setup demo A-Z cho chang 07

Trang thai: `ACTIVE`

Tai lieu nay huong dan chay demo bang shell script, khong can mo tung project thu cong.

## 1. Dieu kien can co

- Windows 10/11.
- .NET Framework 4.7.2 Developer Pack va Visual Studio Build Tools ho tro WinForms/MSBuild.
- PostgreSQL hoac Neon da co database `drawingapp`.
- `server.pfx` co san trong `DrawingServer`.
- Ngrok phai la phien ban `3.20.0` tro len tren may chay LoadBalancer.
- Tai khoan ngrok da duoc xac thuc va co authtoken hop le tren may chay LoadBalancer.
- Neu backend khac mang, tai khoan Tailscale va 2 server da join cung 1 tailnet.
- Neu `ngrok` khong co trong `PATH`, co the dat `NGROK_PATH` hoac `NGROK_EXE` tro toi file `ngrok.exe`.

## 2. File can chuan bi

- Copy `.env.example` thanh `.env` o root repo.
- Dien cac gia tri can thiet cho client, LB va server.
- Neu chay demo public, dam bao `USE_LOAD_BALANCER_ROUTING=1` va `LOAD_BALANCER_CLIENT_MODE=relay`.

## 2.1. Yeu cau theo role

### Client

- Can `DrawingClient`.
- Can `SharedLib` cung cap ben canh `DrawingClient` neu chay tu source.
- Can `local/setup` de co script client.
- Neu chay tu source, can `.NET Framework 4.7.2` hoac moi truong build tuong thich.
- Khi shell chay, script se hoi `LB host` va `LB port` neu chua co san.
- Muon co them client thi rerun lai script client trong shell moi.

### Server

- Can `DrawingServer`.
- Can `SharedLib` ben canh `DrawingServer` neu chay tu source.
- Can `local/setup` de co script server.
- Neu chay 2 server tren cung may, phai de `SERVER_ID`, `SERVER_TCP_PORT`, `SERVER_UDP_PORT`, `SERVER_LOG_FILE` khac nhau.

### LoadBalancer

- Can `LoadBalancer`.
- Can `SharedLib` ben canh `LoadBalancer` neu chay tu source.
- Can `local/setup` de co script load balancer.
- Khi shell chay, script se hoi IP/port cua 2 server backend neu chua khai bao san.
- Neu may chay LoadBalancer chua co `ngrok`, cai nhanh bang PowerShell/winget:

```powershell
winget install -e --id Ngrok.Ngrok
$ngrokExe = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Filter ngrok.exe -Recurse | Select-Object -First 1
if ($null -ne $ngrokExe) {
	$ngrokDir = Split-Path $ngrokExe.FullName
	if (($env:Path -split ';') -notcontains $ngrokDir) {
		$env:Path = "$env:Path;$ngrokDir"
		[Environment]::SetEnvironmentVariable('Path', $env:Path, 'User')
	}
}
```

- Neu da co `ngrok` roi, co the update truoc khi chay:

```powershell
winget upgrade -e --id Ngrok.Ngrok
$ngrokExe = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Filter ngrok.exe -Recurse | Select-Object -First 1
if ($null -ne $ngrokExe) {
	$ngrokDir = Split-Path $ngrokExe.FullName
	if (($env:Path -split ';') -notcontains $ngrokDir) {
		$env:Path = "$env:Path;$ngrokDir"
		[Environment]::SetEnvironmentVariable('Path', $env:Path, 'User')
	}
}
```

- Sau khi cai, neu shell hien tai chua thay `ngrok`, dong/mo lai PowerShell hoac chay lai dong lenh refresh `PATH` o tren.

- Neu ngrok chua co authtoken, cai dat 1 lan tren may LoadBalancer:

```powershell
ngrok config add-authtoken <your-ngrok-authtoken>
```

- Authtoken nay chi can cau hinh mot lan cho user hien tai tren may LB. Khong commit token vao repo va khong truyen qua client.

### Ghi chu

- Khong can copy ca 3 project len moi may. May nao lam role nao thi chi can project cua role do + `SharedLib` + `local/setup` helper.
- Neu mot may dong thoi nhieu role, mo nhieu shell rieng va chay script role tuong ung trong tung shell.
- Cua so PowerShell cua client chi la launcher. Sau khi `client.exe` da mo xong, ban co the dong cua so PowerShell neu app client van con chay trong may ban dang test; neu muon chan 100% thi giu lai cua so launcher.
- Gia tri env do script set cho moi role co uu tien hon `.env` chung, nen server 2 se giu `SERVER_ID=server-2` va port rieng ngay ca khi trong root repo co `.env` co san.

## 2.2. 3 shell script khoi dong

Tat ca script nam trong `local/setup` va duoc chay tu root repo bang PowerShell. Script se mo cac file `.exe` da build san trong `bin\\Debug` thay vi goi `dotnet run`, nen hop voi WinForms/WinExe va project .NET Framework cu:

```powershell
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-1-local.ps1
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-2-lan.ps1 -Role LoadBalancer -Server1Host <ip-server-1> -Server2Host <ip-server-2>
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-3-internet.ps1 -Role LoadBalancer -StartNgrok -Server1Host <tailscale-ip-server-1> -Server2Host <tailscale-ip-server-2>
```

Ket cau chung:

- `scenario-1-local.ps1`: 1 may, 2 server + LB + 3 client.
- `scenario-2-lan.ps1`: nhieu may cung LAN.
- `scenario-3-internet.ps1`: nhieu may khac mang, server qua Tailscale, client vao LB qua ngrok.

Luu y:

- Tu ngay 2026-05-25, LoadBalancer mac dinh dung `LOAD_BALANCER_STRATEGY=room-affinity` trong relay mode: client join room cu se hoi `ROUTE room=<roomCode>`, sau do reconnect bang `RELAY server=<owner_server_id>` de vao dung server owner cua room. Room moi/route khong co owner se chon backend it tai hon. Cach nay giu client cung room tren cung server de tranh di vong PostgreSQL `LISTEN/NOTIFY`, dong thoi khong don tat ca room moi vao server 1.
- LoadBalancer can chay ban build moi dung `Npgsql 8.0.9`; neu thay log `Couldn't set ssl mode`, hay build lai `LoadBalancer` va dam bao dang chay `LoadBalancer\bin\Debug\LoadBalancer.exe` moi.
- Kich ban 1 co the chay them client phu bang cach rerun `scenario-1-local.ps1 -Role Client`.
- Kich ban 2 va 3 co the chay moi role trong mot shell rieng.

## 3. Kich ban 1: tat ca tren cung 1 may

### May can chay

- Chi 1 may duy nhat.

### Shell can chay

```powershell
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-1-local.ps1
```

Neu can them client phu tren cung may, mo shell moi va chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-1-local.ps1 -Role Client -ClientLabel Client-4
```

### Script se tu dong mo

- `DrawingServer` 1: TCP `8888`, UDP `8889`, log `server_logs_server-1.txt`.
- `DrawingServer` 2: TCP `8890`, UDP `8891`, log `server_logs_server-2.txt`.
- `LoadBalancer`: port `9000`.
- `Client 1`, `Client 2`, `Client 3`.

- Scenario 1 khong can ngrok. Day la demo local thuần: 2 server + LB + 3 client tren cung 1 may.

- Neu can demo public o kich ban 3, may chay LB moi can `ngrok` trong `PATH`.

### Luu y port va log

- Port server phai khac nhau: `8888/8889` va `8890/8891`.
- Port LB van la `9000`.
- Hai server tren cung may phai co `SERVER_LOG_FILE` khac nhau de tranh lock file log.
- Cac script se tu don process dang giu port demo cu truoc khi mo process moi. Neu van co lock, kiem tra xem co process ngoai script dang giu port hay khong.

## 4. Kich ban 2: client + LB + server tren nhieu may cung LAN

### May can chay

- May LB: chay `scenario-2-lan.ps1 -Role LoadBalancer`.
- May server 1: chay `scenario-2-lan.ps1 -Role Server1`.
- May server 2: chay `scenario-2-lan.ps1 -Role Server2`.
- May client 1: chay `scenario-2-lan.ps1 -Role Client -LbHost <ip-lb-lan>`.
- May client 2: chay `scenario-2-lan.ps1 -Role Client -LbHost <ip-lb-lan>`.
- May client 3: chay `scenario-2-lan.ps1 -Role Client -LbHost <ip-lb-lan>`.

### Shell can chay

May LB:

```powershell
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-2-lan.ps1 -Role LoadBalancer -Server1Host <ip-server-1> -Server2Host <ip-server-2>
```

May server 1:

```powershell
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-2-lan.ps1 -Role Server1
```

May server 2:

```powershell
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-2-lan.ps1 -Role Server2
```

May client 1/2/3:

```powershell
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-2-lan.ps1 -Role Client -LbHost <ip-lb-lan> -ClientLabel Client-1
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-2-lan.ps1 -Role Client -LbHost <ip-lb-lan> -ClientLabel Client-2
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-2-lan.ps1 -Role Client -LbHost <ip-lb-lan> -ClientLabel Client-3
```

### Script se tu dong lam

- May LB se tao `LoadBalancer\servers.json` voi 2 backend LAN.
- Moi client se ket noi relay vao LB LAN qua port `9000`.
- Scenario 2 chay noi bo/LAN, khong bat buoc ngrok; chi can ngrok neu ban muon client public di vao LB qua internet.

### Luu y port va log

- `LoadBalancer` van nghe `9000`.
- 2 backend server phai dung 2 cap port TCP/UDP khac nhau.
- Neu 1 may chay 2 server, khong duoc dung chung `SERVER_LOG_FILE`.

## 5. Kich ban 3: client + LB + server tren nhieu may khac mang

### May can chay

- May LB: chay `scenario-3-internet.ps1 -Role LoadBalancer -StartNgrok`.
- May server 1: chay `scenario-3-internet.ps1 -Role Server1`.
- May server 2: chay `scenario-3-internet.ps1 -Role Server2`.
- May client 1: chay `scenario-3-internet.ps1 -Role Client -LbHost <ngrok-host> -LbPort <ngrok-port>`.
- May client 2: chay `scenario-3-internet.ps1 -Role Client -LbHost <ngrok-host> -LbPort <ngrok-port>`.
- May client 3: chay `scenario-3-internet.ps1 -Role Client -LbHost <ngrok-host> -LbPort <ngrok-port>`.

### Shell can chay

May LB:

```powershell
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-3-internet.ps1 -Role LoadBalancer -StartNgrok -Server1Host <tailscale-ip-server-1> -Server2Host <tailscale-ip-server-2>
```

May server 1:

```powershell
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-3-internet.ps1 -Role Server1
```

May server 2:

```powershell
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-3-internet.ps1 -Role Server2
```

May client 1/2/3:

```powershell
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-3-internet.ps1 -Role Client -LbHost <ngrok-host> -LbPort <ngrok-port> -ClientLabel Client-1
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-3-internet.ps1 -Role Client -LbHost <ngrok-host> -LbPort <ngrok-port> -ClientLabel Client-2
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-3-internet.ps1 -Role Client -LbHost <ngrok-host> -LbPort <ngrok-port> -ClientLabel Client-3
```

### Script se tu dong lam

- May LB se tao `LoadBalancer\servers.json` voi 2 backend Tailscale.
- May LB se mo them cua so `ngrok tcp 9000`.
- Trước khi mo `ngrok tcp 9000`, may LB phai co `ngrok config add-authtoken <your-ngrok-authtoken>` hoac da dang nhap authtoken tu truoc.
- Neu phien ban hien tai chua dat `3.20.0+`, hay chay `winget upgrade -e --id Ngrok.Ngrok` truoc.
- Neu dung ngrok reserved endpoint, co the dien host/port co dinh cho client.
- Neu dung ngrok tam thoi, lay host/port tu dong `Forwarding` va copy sang 3 lenh client.

### Luu y port va internet

- LB van dung `9000`.
- Khong expose TCP/UDP cua server truc tiep ra internet.
- Client di qua ngrok vao LB, khong can cai Tailscale.
- Neu may LB khong co `ngrok` trong `PATH`, dat `NGROK_PATH` hoac `NGROK_EXE` truoc khi chay script.
- Neu may LB khong co `ngrok` trong `PATH`, dung `winget install -e --id Ngrok.Ngrok` roi chay dong lenh refresh `PATH` o tren.
- Neu da cai xong ma van gap `ERR_NGROK_4018`, kiem tra lai authtoken va user ngrok hien tai.

## 6. Lenh build/test can chay truoc demo

Chay tu root repo:

```powershell
dotnet restore .\NT106_DrawingApp.sln /p:RestorePackagesConfig=true
dotnet build .\NT106_DrawingApp.sln -v:minimal
dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal
```

Ket qua mong doi:

- Build pass.
- Test pass 18, skip 1.

Luu y:

- Nen build xong truoc khi mo cac server.
- Neu dang chay `DrawingServer` ma build lai, MSBuild co the bao file exe bi lock.
- Neu can rebuild, hay stop tat ca server dang chay truoc.
- Sau khi sua LoadBalancer/client, phai dong cac cua so scenario cu va chay lai script de tien trinh moi nap code/env moi.

## 7. Checklist truoc khi demo

- Database da co schema va credential dung.
- Da build xong truoc khi mo cac server.
- Neu chay 2 server tren cung may, da set `SERVER_ID`, `SERVER_TCP_PORT`, `SERVER_UDP_PORT`, `SERVER_LOG_FILE` khac nhau.
- LoadBalancer dang nghe port `9000`.
- May LB da co `servers.json` dung voi topology dang chay.
- Neu demo internet, ngrok dang forward vao LoadBalancer.
- Client `.env` hoac env do script set da dung `relay` mode.
- 3 client da co the dang nhap va vao cung 1 room.

## 8. Khi demo xong

- Ghi lai topology that da chay.
- Ghi log ngrok endpoint.
- Ghi server nao nam LAN, server nao qua Tailscale.
- Cap nhat `local/plan/status_check.md`, `local/features.md`, va `local/plan/11_nhat_ky_thuc_thi_2026-05-24.md` neu user xac nhan pass.
