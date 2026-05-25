# 10 - Runbook chay thu tung chang va demo tong

Trang thai: `ACTIVE`

## Muc tieu

Huong dan user chay thu sau khi Codex bao `WAITING_USER`.

## Cach chay thu mot chang

1. Doc muc `User chay thu` trong file chang.
2. Keo code moi nhat trong workspace.
3. Chay app theo dung cau hinh chang.
4. Ghi lai:
   - dat/chua dat
   - buoc nao loi
   - log console/server/LB neu co
   - anh man hinh neu UI loi
5. Tra loi cho Codex:
   - `Dat chang <ten chang>` neu pass
   - hoac mo ta loi neu fail

## Mau user xac nhan pass

```text
Dat chang 07 LB relay.
Da test 2 client qua LB local, draw/fill/text/chat/member list deu sync.
Chua test ngrok.
```

## Mau user bao loi

```text
Chang 07 chua dat.
Buoc: client 2 join room.
Hien tuong: login duoc nhung draw khong sync.
Log server: ...
Log LB: ...
```

## Runbook demo tong

Runbook day du cho cac kich ban demo nam o [local/setup/setup.md](../setup/setup.md).

### Chuan bi

- `.env` tren moi may.
- Neon connection string dung.
- `server.pfx` co san cho DrawingServer.
- Port Windows Firewall mo cho backend.
- Ngrok TCP tunnel san sang.

### Cau hinh client public

```env
USE_LOAD_BALANCER_ROUTING=1
LOAD_BALANCER_CLIENT_MODE=relay
LOAD_BALANCER_STRATEGY=room-affinity
LOAD_BALANCER_HOST=<ngrok-host>
LOAD_BALANCER_PORT=<ngrok-port>
```

### Cau hinh LB

```env
LOAD_BALANCER_PORT=9000
LOAD_BALANCER_STRATEGY=room-affinity
DATABASE_URL=<neon>
LB_SERVER_1_HOST=<lan-or-tailscale-host-server-1>
LB_SERVER_1_TCP_PORT=8888
LB_SERVER_2_HOST=<lan-or-tailscale-host-server-2>
LB_SERVER_2_TCP_PORT=8890
```

### Cau hinh server

Server 1:

```env
SERVER_ID=server-1
SERVER_TCP_PORT=8888
SERVER_UDP_PORT=8889
DATABASE_URL=<neon>
```

Server 2:

```env
SERVER_ID=server-2
SERVER_TCP_PORT=8890
SERVER_UDP_PORT=8891
DATABASE_URL=<neon>
```

### Thu tu test

1. Chay 2 server.
2. Chay LB.
3. Mo ngrok vao LB.
4. Mo 3 client public.
5. Login/register.
6. Client 1 tao room.
7. Client 2/3 join room.
8. Test draw/fill/text/chat/member list.
9. Tat server owner, thu client moi join/reconnect.
10. Test 10 client neu co du may.

## Mau cap nhat sau demo

```md
### Ket qua demo YYYY-MM-DD

- Topology: LAN/Tailscale.
- Ngrok: host:port.
- 3 client: pass/fail.
- 2 server: pass/fail.
- Failover: pass/fail.
- 10 client: pass/fail.
- Loi con lai:
  - ...
```

## Cau hoi can chot

- User chay demo tren may nao va topology LAN hay Tailscale?
- User co ngrok reserved endpoint hay endpoint tam thoi?
- Khi demo fail, user co the gui log nao?

## Ke hoach trien khai code sau khi user chay runbook

1. Neu user bao pass, chi cap nhat status/features.
2. Neu user bao fail, doc buoc fail va log.
3. Khoanh vung vao file chang lien quan.
4. Sua code theo chang do, khong sua lan sang chang khac.
5. Chay lai test Codex roi tra user runbook moi neu can.

## Kiem thu Codex phai chay

- Theo chang bi anh huong.
- Neu chi sua runbook, khong can build.

## User chay thu

User dung runbook nay de chay manual test va tra feedback pass/fail.

## Cap nhat sau khi user xac nhan

- `status_check.md`: trang thai chang.
- `features.md`: tinh nang pass/fail.
- `11`: ket qua demo.
