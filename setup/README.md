# NT106 Drawing App setup

Moi may dung cung mot goi `NT106-DrawingApp-setup.zip`. Giai nen xong, dung PowerShell tai thu muc cha cua `setup`.

```text
<demo-folder>\setup\.env.example
<demo-folder>\setup\apps\DrawingClient\DrawingClient.exe
<demo-folder>\setup\apps\DrawingServer\DrawingServer.exe
<demo-folder>\setup\apps\LoadBalancer\LoadBalancer.exe
```

## Buoc bat buoc tren moi may

Copy env:

```powershell
Copy-Item .\setup\.env.example .\setup\.env
notepad .\setup\.env
```

Dien secret trong `setup\.env` bang kenh an toan. Script khong hoi database URL, cert password hay API key tren man hinh.

Port mac dinh:

| Role | TCP | UDP |
| --- | ---: | ---: |
| server-1 | 8888 | 8889 |
| server-2 | 8890 | 8891 |
| LoadBalancer | 9000 | 9001 |

Script khong hoi port. Neu can doi port, sua trong `setup\.env` truoc khi chay.

## Kich ban 1: Mot may, khong LoadBalancer

Dung khi tat ca chay tren mot may: 1 server + 3 client.

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-local-no-lb.ps1 -StopExisting
```

Chi kiem tra server co mo port, khong mo client:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-local-no-lb.ps1 -ClientCount 0 -StopExisting
```

Script se doi server bang TLS handshake va in `server-1 san sang TLS tai 127.0.0.1:8888`. Client do script mo se chay direct vao server local, khong di qua LoadBalancer, ke ca khi `setup\.env` mac dinh dang de `USE_LOAD_BALANCER_ROUTING=1` cho cac kich ban LB.

Mac dinh `127.0.0.1` nghia la client va server cung mot may.

## Kich ban 2: Mot may, co LoadBalancer

Dung khi tat ca chay tren mot may: 2 server + 1 LoadBalancer + 3 client.

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-local-with-lb.ps1 -StopExisting
```

Chi kiem tra 2 server va LoadBalancer co mo port, khong mo client:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-local-with-lb.ps1 -ClientCount 0 -StopExisting
```

Mac dinh `127.0.0.1` nghia la LoadBalancer va 2 server cung mot may.

## Kich ban 3: LAN direct, khong LoadBalancer

May server chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-server.ps1 -ServerId server-1 -StopExisting
```

May client chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-client.ps1 -Mode Direct -Host <server-lan-ip>
```

`<server-lan-ip>` lay tren may server bang:

```powershell
ipconfig
```

Neu client cung may server, dung:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-client.ps1 -Mode Direct -Host 127.0.0.1
```

`127.0.0.1` nghia la ket noi vao server tren chinh may dang chay client.

## Kich ban 4: LAN co LoadBalancer

May server-1 sua `setup\.env`:

```env
SERVER_ID=server-1
SERVER_TCP_PORT=8888
SERVER_UDP_PORT=8889
```

May server-1 chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-server.ps1 -StopExisting
```

May server-2 sua `setup\.env`:

```env
SERVER_ID=server-2
SERVER_TCP_PORT=8890
SERVER_UDP_PORT=8891
```

May server-2 chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-server.ps1 -StopExisting
```

May LoadBalancer chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-load-balancer.ps1 -StopExisting
```

Script se hoi:

- `Server-1 host/IP`: IP may server-1. Lay bang `ipconfig`. Enter = `127.0.0.1`, nghia la server-1 chay cung may voi LoadBalancer.
- `Server-2 host/IP`: IP may server-2. Lay bang `ipconfig`. Enter = IP server-1 vua nhap, nghia la server-2 chay cung may voi server-1 nhung port khac.

May client LAN chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-client.ps1 -Mode LbRelay -Host <lb-lan-ip> -EnableLbUdpProxy
```

`<lb-lan-ip>` lay tren may LoadBalancer bang `ipconfig`. Neu client cung may LoadBalancer, dung `127.0.0.1`.

## Kich ban 5.1: Internet ngrok, LoadBalancer va server cung may

May cua ban dong ca LoadBalancer va 2 server.

Chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-local-with-lb.ps1 -ClientCount 0 -StartNgrok -StopExisting
```

Script se mo server-1, server-2, LoadBalancer, roi tu mo ngrok TCP vao LoadBalancer. Neu muon mo ngrok thu cong thay vi de script mo, dung:

```powershell
ngrok tcp 9000
```

Ngrok se hien endpoint dang:

```text
tcp://0.tcp.ap.ngrok.io:14980
```

May client Internet chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-client.ps1 -Mode LbRelay -InternetNgrok -Host 0.tcp.ap.ngrok.io -TcpPort 14980
```

Khong dung UDP trong kich ban Internet ngrok. Client tu dung TCP fallback cho cursor.

Neu dang chay 2 server tren cung may voi LoadBalancer, hay dung kich ban 5.1 nay. Khong can Tailscale.

## Kich ban 5.2: Internet ngrok, LoadBalancer rieng, server cung LAN

May server-1 chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-server.ps1 -ServerId server-1 -StopExisting
```

May server-2 chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-server.ps1 -ServerId server-2 -StopExisting
```

May LoadBalancer chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-load-balancer.ps1 -StartNgrok -StopExisting
```

Script se hoi:

- `Server-1 host/IP`: IP LAN cua may server-1. Lay bang `ipconfig`. Enter = `127.0.0.1`, nghia la server-1 cung may LoadBalancer.
- `Server-2 host/IP`: IP LAN cua may server-2. Lay bang `ipconfig`. Enter = gia tri server-1, nghia la server-2 cung may voi server-1.

Neu ngrok doc duoc endpoint, script se in lenh client mau. May client Internet chay, vi du:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-client.ps1 -Mode LbRelay -InternetNgrok -Host 0.tcp.ap.ngrok.io -TcpPort 14980
```

## Kich ban 5.3: Internet ngrok, LoadBalancer va server khac mang

Dung Tailscale cho duong LoadBalancer -> server. Client Internet khong can Tailscale.

May server-1 va server-2 cai Tailscale, dang nhap cung tailnet, lay IP:

```powershell
tailscale ip -4
```

May server-1 chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-server.ps1 -ServerId server-1 -StopExisting
```

May server-2 chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-server.ps1 -ServerId server-2 -StopExisting
```

May LoadBalancer cung cai Tailscale, roi chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-load-balancer.ps1 -StartNgrok -StopExisting
```

Khi script hoi `Server-1 host/IP` va `Server-2 host/IP`, nhap IP Tailscale lay tu `tailscale ip -4`.

May client Internet chay theo endpoint ngrok:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-client.ps1 -Mode LbRelay -InternetNgrok -Host <ngrok-host> -TcpPort <ngrok-port>
```

## Kich ban 5.4: Internet ngrok, client ket noi thang server

Dung khi chi can 1 server public ra Internet, khong can LoadBalancer. Kich ban nay phu hop demo gon nhe 1 server + nhieu client; khong co chia tai/room-affinity qua 2 server.

May server chay:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-server.ps1 -ServerId server-1 -StartNgrok -StopExisting
```

Neu muon mo ngrok thu cong thay vi de script mo:

```powershell
ngrok tcp 8888
```

May client Internet chay theo endpoint ngrok, vi du:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-client.ps1 -Mode Direct -InternetNgrok -Host 0.tcp.ap.ngrok.io -TcpPort 14980
```

Khong dung UDP trong kich ban Internet direct qua ngrok. Client `-InternetNgrok` tu bat `CLIENT_FORCE_TCP_REALTIME=1`, nen cursor/tin hieu tam thoi dung TCP fallback.

## Ngrok co can refresh moi lan khong?

Khong can refresh neu cua so ngrok/tunnel van dang chay va endpoint `tcp://host:port` van con truy cap duoc. Port nhu `14980` co the giu nguyen neu ban dung reserved/static TCP address cua ngrok, hoac neu tunnel/session hien tai chua bi tat.

Can lay lai host/port ngrok khi:

- Ban tat cua so ngrok roi mo lai bang endpoint tam thoi.
- Ngrok bao session het han hoac reconnect sang endpoint khac.
- May LoadBalancer doi mang va tunnel cu khong con ket noi.

Neu dung ngrok free/tunnel tam thoi, hay nhin lai cua so ngrok moi lan demo va copy dung `tcp://host:port`. Neu dung reserved endpoint, host/port co the co dinh.

## Mot may dong nhieu vai tro

Mot may co the dong nhieu vai tro. Quy tac:

- Role nao chay cung may thi host/IP la `127.0.0.1`.
- Role nao chay khac may trong LAN thi host/IP la IPv4 lay bang `ipconfig`.
- Role nao chay khac mang qua Tailscale thi host/IP la IP lay bang `tailscale ip -4`.
- Client Internet chi biet ngrok host/port cua LoadBalancer, khong can biet IP server.

## Goi y khi loi

- Loi `Cannot overwrite variable Host`: dung goi setup moi sau khi da cap nhat script; `-Host` van dung duoc.
- Neu port bi chiem, them `-StopExisting`.
- Neu server khong mo TCP sau 25 giay, xem cua so server va log trong `setup\apps\DrawingServer`.
- Neu client crash hoac hien loi sau login/join room, xem `setup\apps\DrawingClient\logs\client_log.txt`.
- Neu client Internet khong ket noi duoc, ngrok phai la TCP tunnel vao LoadBalancer port `9000`, khong phai HTTP tunnel.
- Neu client cung room bi route sai backend, kiem tra `servers.json` trong `setup\apps\LoadBalancer`, `SERVER_ID` tren server va `Rooms.owner_server_id` trong database phai khop nhau. Neu LB bao `room was not found in LoadBalancer database`, server va LoadBalancer dang khong dung cung `DATABASE_URL`.
