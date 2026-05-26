# Checklist demo

## Chuan bi moi may

- [ ] Da giai nen `NT106-DrawingApp-setup.zip`.
- [ ] Da copy `setup\.env.example` thanh `setup\.env`.
- [ ] Secret trong `setup\.env` da duoc dien bang kenh an toan.
- [ ] `setup\apps\DrawingClient\DrawingClient.exe` ton tai.
- [ ] `setup\apps\DrawingServer\DrawingServer.exe` ton tai.
- [ ] `setup\apps\LoadBalancer\LoadBalancer.exe` ton tai.

## Local

- [ ] `start-local-no-lb.ps1 -ClientCount 0 -StopExisting` mo duoc server-1.
- [ ] `start-local-with-lb.ps1 -ClientCount 0 -StopExisting` mo duoc server-1, server-2 va LB.
- [ ] 3 client local login/join/draw/chat pass.

## LAN

- [ ] Server-1 chay voi `SERVER_ID=server-1`.
- [ ] Server-2 chay voi `SERVER_ID=server-2` hoac lenh `-ServerId server-2`.
- [ ] LoadBalancer chi hoi IP server, khong hoi port.
- [ ] Client LAN ket noi `LbRelay -Host <lb-lan-ip>` pass.

## Internet ngrok

- [ ] Ngrok CLI cai tren may LoadBalancer.
- [ ] Neu LB va 2 server cung may: `start-local-with-lb.ps1 -ClientCount 0 -StartNgrok -StopExisting` tao endpoint `tcp://host:port`.
- [ ] Neu LB rieng server: `ngrok tcp 9000` hoac `start-load-balancer.ps1 -StartNgrok` tao endpoint `tcp://host:port`.
- [ ] Client Internet chay `start-client.ps1 -Mode LbRelay -InternetNgrok -Host <ngrok-host> -TcpPort <ngrok-port>`.
- [ ] Client Internet khong bat UDP proxy; cursor dung TCP fallback.
- [ ] Neu LB khac mang server, LB va server dung Tailscale IP cho duong backend.
- [ ] Neu app client loi/crash, xem `setup\apps\DrawingClient\logs\client_log.txt`.

## Internet direct server

- [ ] Server chay `start-server.ps1 -ServerId server-1 -StartNgrok -StopExisting` hoac ngrok thu cong `ngrok tcp 8888`.
- [ ] Client Internet chay `start-client.ps1 -Mode Direct -InternetNgrok -Host <ngrok-host> -TcpPort <ngrok-port>`.
- [ ] Client direct ngrok khong dung UDP; `CLIENT_FORCE_TCP_REALTIME=1` de cursor dung TCP fallback.
- [ ] Login/join/draw/chat pass voi 1 server, khong qua LoadBalancer.

## Muc tieu demo

- [ ] 3 client + 2 server + 1 LB chay dong thoi.
- [ ] Draw/fill/text khong mat net.
- [ ] Chat/member list pass.
- [ ] Client join sau nhan lai board history dung.
