# 07 - Ngrok, load balancer, Tailscale va multi-server

Trang thai: `TESTED_BY_CODEX`

## Muc tieu

Demo chot: 3 client khac mang -> ngrok -> LoadBalancer -> 2 drawing server -> Neon. LoadBalancer la public ingress duy nhat.

Hai kieu backend duoc ho tro trong demo:

- Ca 2 server o cung LAN: co the chay 1 may dong thoi LoadBalancer + 2 server, khong can Tailscale.
- 2 server o 2 mang khac nhau: LoadBalancer tro backend qua Tailscale.

## Pham vi code

- `LoadBalancer/*`
- `DrawingClient/Forms/LoginForm.cs`
- `DrawingClient/Network/ClientNetwork.cs`
- `DrawingClient/Network/LoadBalancerRouteClient.cs`
- `DrawingServer/Network/SecureTcpServer.cs`
- `DrawingServer/Services/CrossServerSyncService.cs`
- `.env.example`

## Da thuc hien gan day

- Them `LOAD_BALANCER_CLIENT_MODE=relay`.
- Mode `relay`: client ket noi TCP vao LB/ngrok, LB proxy session den backend.
- Mode `direct`: client goi `ROUTE` de ket noi truc tiep backend, chi dung LAN/Tailscale.
- Cap nhat `room-affinity`: client join room goi `ROUTE room=<roomCode>`, LB lay `Rooms.owner_server_id`, client reconnect qua LB bang `RELAY server=<server_id>` de dam bao cung room vao cung backend. Neu khong co owner route thi LB chon backend it tai hon.
- Dong bo thu vien DB: LoadBalancer dung `Npgsql 8.0.9` giong DrawingServer.
- Toi uu realtime relay: TCP draw/flood fill/text/spray broadcast truoc, luu DB nen sau de Neon khong chen do tre vao net ve.
- Draw/flood fill/text co TCP fallback trong mode relay.
- Server TCP broadcast draw/flood fill/text truoc, luu DB nen sau.
- Cross-server sync van dung `LISTEN/NOTIFY`.
- Build pass 0 warning, test pass 18 skip 1.

## Cau hoi da chot

- Demo co the chay 2 server cung LAN, hoac 2 server o mang khac nhau qua Tailscale.
- Ngrok dung nhu cong vao public internet cho LoadBalancer.
- Room-aware routing trong relay can uu tien cho demo toc do cao: cung room phai bam owner server, room moi/khong co owner moi duoc chia theo tai.

## Ke hoach trien khai code tiep theo

1. Neu user test relay fail, sua LB/client theo log.
2. Them log LB ro hon: backend selected, active connections, health state.
3. Them test/utility health check neu can.

## Kiem thu Codex phai chay

- `dotnet build .\NT106_DrawingApp.sln -v:minimal`
- `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal`
- Neu sua package: `dotnet list .\DrawingServer\DrawingServer.csproj package --vulnerable`

## User chay thu

### Local relay smoke

1. Chay LoadBalancer port `9000` va 2 server backend.
2. Neu backend chung 1 may LAN, dung IP local `127.0.0.1` hoac IP LAN cho ca 2 server.
3. Neu backend khac mang, day IP Tailscale cua tung server vao cau hinh LB.
4. Client `.env`:
   - `USE_LOAD_BALANCER_ROUTING=1`
   - `LOAD_BALANCER_CLIENT_MODE=relay`
   - `LOAD_BALANCER_STRATEGY=room-affinity`
   - `LOAD_BALANCER_HOST=127.0.0.1`
   - `LOAD_BALANCER_PORT=9000`
5. Mo 3 client, login, tao/join room.
6. Thu draw/flood fill/text/chat/member list, sau do dong mot backend va quan sat fallback/cross-server sync.

### Demo internet

1. Chay 2 server voi `SERVER_ID` va port rieng.
2. LB backend tro den IP LAN neu 2 server cung mang, hoac IP Tailscale neu khac mang.
3. `ngrok tcp 9000`.
4. Client public tro den host/port ngrok.
5. Test 3 client khac mang, failover, reconnect.

## Cap nhat sau khi user xac nhan

- `features.md`: load balancer/public internet/Tailscale neu da test.
- `status_check.md`: chuyen muc 07 sang `ACCEPTED` sau khi demo pass.
- `11`: ghi endpoint/topology/ket qua.

## Luu y/rui ro

- Trong relay TLS, LB van khong giai ma payload `JOIN_ROOM`; route dung room duoc thuc hien bang preflight `ROUTE room=<roomCode>` va preface `RELAY server=<server_id>` truoc TLS handshake.
- Cross-server sync la fallback bat buoc khi client cung room bi chia backend, nhung flow join room moi da tranh truong hop nay trong duong chinh.
- UDP qua ngrok TCP khong san sang; draw/flood fill/text da co TCP fallback, nhung cursor/laser realtime van phu thuoc UDP/LAN neu chua co fallback.
