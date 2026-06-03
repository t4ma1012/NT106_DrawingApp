# NT106 Drawing App

Ung dung ve cong tac thoi gian thuc bang C# WinForms, ho tro nhieu nguoi cung ve, chat, luu gallery va su dung AI image trong cung mot phong. Du an duoc xay theo mo hinh Client - Server, co Load Balancer cho demo nhieu server va PostgreSQL de luu tai khoan, phong, lich su ve va du lieu cong tac.

## 1. Kien truc tong quan

Du an gom 4 project chinh:

| Project | Vai tro |
| --- | --- |
| `DrawingClient` | Ung dung WinForms cho nguoi dung: dang nhap/dang ky, tao/join phong, ve canvas, chat, gallery, AI image, sticker, text, sticky note. |
| `DrawingServer` | Server TCP/TLS + UDP/AES: xu ly packet, quan ly room, broadcast realtime, luu PostgreSQL. |
| `LoadBalancer` | Ingress/proxy cho nhieu DrawingServer: route client, health check server, room-affinity, relay TCP va UDP proxy local/LAN. |
| `SharedLib` | Thu vien dung chung: packet protocol, payload DTO, AES helper, env loader, logger, API config. |

Topology demo khuyen nghi:

```text
Client(s) -> ngrok TCP/LAN -> LoadBalancer -> DrawingServer-1/2 -> PostgreSQL
```

Voi demo 1 server don gian:

```text
Client(s) -> DrawingServer -> PostgreSQL
```

## 2. Cau truc thu muc

```text
NT106_DrawingApp/
+-- DrawingClient/                 # WinForms client
|   +-- Forms/                       # Login, Lobby, Main canvas, Gallery
|   +-- Drawing/                     # CanvasManager, tools, flood fill, text tool
|   +-- Network/                     # TCP/TLS, UDP/AES, route client, event hub
|   +-- AI/                          # Hugging Face image, Remove.bg
+-- DrawingServer/                 # Secure TCP/UDP server
|   +-- Network/                     # SecureTcpServer, SecureUdpServer, ClientSession
|   +-- Services/                    # Room/Auth/Draw/Cross-server/Heartbeat
|   +-- Services/Database/           # DbManager, queue, migrations
+-- LoadBalancer/                  # TCP/UDP proxy, route, health check
+-- SharedLib/                     # Packet, payload, security, config, logger
+-- NT106Tests/                    # Unit/load/security tests
+-- setup/                         # Scripts va binary release cho demo
+-- local/                         # Tai lieu noi bo, plan, overview
+-- .env.example                   # Mau env root
+-- NT106_DrawingApp.sln           # Solution chinh
```

`setup/apps`, `bin`, `obj`, `local/tmp_build` la output build/runtime, khong phai source logic chinh.

## 3. Tinh nang chinh

### Tai khoan va phong

- Dang nhap/dang ky qua TCP/TLS.
- Password duoc bam SHA-256 truoc khi luu DB.
- Tao phong bang ma phong 6 so.
- Join phong co gioi han so thanh vien, mac dinh `MAX_ROOM_MEMBERS=5`.
- Room owner duoc luu trong DB va gan voi `owner_server_id` de LoadBalancer route dung server.

### Canvas va cong cu ve

- Canvas GDI+ co dinh 1920x1080.
- Pen, line, rectangle, circle, eraser, pipette, flood fill BFS.
- Giu `Shift` khi ve rectangle/circle de tao hinh vuong/hinh tron.
- Text tool luu toa do theo canvas, co the move/resize/delete.
- Import image, sticker, sticky note, background mau/anh.
- Zoom/pan local, moi user tu dieu chinh viewport rieng.
- Undo/redo theo action cua chinh user.
- Clear all xoa canvas va lich su phong.

### Cong tac realtime

- Draw/fill/text/import/sticker/background di TCP reliable de tranh mat net.
- Cursor/pixel/realtime tam thoi co the di UDP/AES trong LAN/direct, hoac TCP fallback qua ngrok/LB.
- Client join sau nhan `SYNC_BOARD` tu history DB va pending queue trong RAM.
- History lon duoc chia chunk de tranh packet qua lon.

### Chat va gallery

- Chat realtime trong phong, luu `ChatHistory`.
- Client join sau nhan lai cac tin gan nhat.
- Save canvas vao Gallery trong DB.
- Xuat anh local khong watermark.
- Gallery co thumbnail/base64 va public token.

### AI

- Text-to-image qua Hugging Face Routing endpoint.
- Remove background qua Remove.bg.
- Ket qua AI duoc chen vao canvas, sync qua server, luu `AiResults` va replay nhu image action.

### Ha tang demo

- Direct local/LAN: client ket noi thang DrawingServer.
- Local/LAN co LoadBalancer: client vao LB, LB chon backend.
- Internet ngrok: public TCP vao LB hoac server direct.
- Tailscale dung cho duong LoadBalancer -> server khi server khac mang.

### Luong dang nhap / dang ky

Luong xac thuc duoc tach thanh 4 lop ro rang de UI khong bi block va server co the xu ly dong bo:

1. `DrawingClient/Forms/LoginForm.cs`
	- Ham `BtnLogin_Click()` xu ly nut `Đăng nhập`, con event `btnRegister.Click` xu ly nut `Đăng ký`.
	- `EnsureConnectedAsync()` dam bao client da ket noi TCP/TLS toi server hoac LoadBalancer truoc khi gui auth.
	- `NetworkEvents_OnLoginResponse()` nhan `LOGIN_RESPONSE` va chuyen sang `LobbyForm` khi login thanh cong.

2. `DrawingClient/Network/ClientNetwork.cs`
	- `SendLogin()` va `SendRegister()` luu `CurrentUsername` va `_lastPassword` de phuc vu reconnect/route ve sau.
	- Hai ham nay dong goi du lieu thanh `LOGIN` / `REGISTER` packet roi gui qua `SslStream`.
	- `ReconnectToRoomOwnerViaLoadBalancerAsync()` dung lai thong tin auth da luu de login lai sau khi route sang owner server.

3. `DrawingServer/Network/SecureTcpServer.cs`
	- `HandleClientAsync()` nhan `LOGIN` hoac `REGISTER` packet trong vong lap doc stream TLS.
	- Packet duoc parse sang payload, sau do goi `DbManager.LoginAsync()` de kiem tra thong tin trong PostgreSQL.
	- Server tra ve `LOGIN_RESPONSE` hoac `REGISTER_RESPONSE` bang `SendPacketToClientAsync()`.
	- Khi dang nhap thanh cong, `session.Username` duoc gan de cac lenh room/chat/draw sau do biet user hien tai.

4. `DrawingServer/Services/Database/DbManager.cs`
	- `LoginAsync()` query `Users.password_hash` theo `username`.
	- `ComputeSha256Hash()` bam mat khau SHA-256 truoc khi so sanh voi gia tri trong DB.
	- Neu user da ton tai thi so sanh hash de xac thuc.
	- Neu user chua ton tai, he thong auto-register bang cach tao ban ghi moi trong `Users`.

Tom lai:

`LoginForm` -> `ClientNetwork.SendLogin/SendRegister` -> `SecureTcpServer` -> `DbManager.LoginAsync` -> `LOGIN_RESPONSE` / `REGISTER_RESPONSE` -> `LobbyForm` neu login thanh cong.

### Luong nhap / xuat du lieu

Mot so luong nhap/xuat quan trong trong ung dung:

1. Nhap tai khoan / mat khau
	- Nguoi dung nhap tren `DrawingClient/Forms/LoginForm.cs`.
	- Client gui `LOGIN` hoac `REGISTER` packet qua `DrawingClient/Network/ClientNetwork.cs`.
	- Server tra ve `LOGIN_RESPONSE` / `REGISTER_RESPONSE` trong `DrawingServer/Network/SecureTcpServer.cs`.

2. Nhap prompt AI, xuat ket qua anh
	- Nguoi dung nhap mo ta anh trong `DrawingClient/AI/StabilityAiClient.cs`.
	- `GenerateImageAsync()` gui prompt len AI service va nhan ve du lieu anh.
	- Ket qua tra ve duoc chen vao canvas qua `DrawingClient/Forms/MainForm.cs` va co the luu vao gallery.

3. Nhap du lieu ve canvas, xuat len cac client khac
	- Nguoi dung ve line, rectangle, circle, text, sticker, flood fill hoac import anh trong `DrawingClient/Drawing/CanvasManager.cs`.
	- `ClientNetwork.Send(...)` dong goi action thanh packet va gui len server.
	- `SecureTcpServer.BroadcastToRoomAsync()` broadcast lai cho cac thanh vien trong room de dong bo realtime.

4. Nhap chat, xuat lich su chat
	- Nguoi dung goi tin nhan trong `DrawingClient/Forms/MainForm.cs`.
	- Server luu vao `ChatHistory` tren may server qua `DrawingServer/Services/Database/DbManager.cs` va gui broadcast cho client dang online.
	- Comment trong `DbManager.SaveChatMessageAsync()` ghi ro day la luu chat history vao PostgreSQL tren may server, khong phai local client.
	- Client moi vao phong se nhan lai mot phan lich su gan nhat trong `SecureTcpServer.HandleClientAsync()`.

5. Nhap / xuat gallery
	- Nguoi dung save canvas thanh anh gallery trong `DrawingClient/Forms/MainForm.cs`.
	- Server luu metadata va anh vao DB qua `DrawingServer/Services/Database/DbManager.cs`.
	- Client co the xem thumbnail, tai lai, hoac xuat anh local khong watermark tu `DrawingClient/Forms/GalleryForm.cs`.

6. Nhap tao / join phong, xuat trang thai phong
	- Nguoi dung tao phong trong `DrawingClient/Forms/LobbyForm.cs`.
	- `ClientNetwork` gui `CREATE_ROOM` va `JOIN_ROOM` packet len `DrawingServer/Network/SecureTcpServer.cs`.
	- Server tra ve `CREATE_ROOM_RESPONSE` / `JOIN_ROOM_RESPONSE` va cap nhat danh sach thanh vien phong.

7. Nhap du lieu local, xuat file / du lieu luu tru
	- Nguoi dung co the luu canvas, gallery, hoac xuat anh local tu `DrawingClient/Forms/MainForm.cs` va `DrawingClient/Forms/GalleryForm.cs`.
	- `DrawingServer/Services/Database/DbManager.cs` luu du lieu xuong PostgreSQL.
	- `DrawingClient/Network/ClientNetwork.cs` va `DrawingServer/Network/SecureTcpServer.cs` dong bo lai trang thai sau khi tai lai phong hoac reconnect.

### Luong stream / TCP-TLS

Mot so file xu ly stream can chu y:

| File | Luong stream chinh |
| --- | --- |
| `DrawingClient/Network/ClientNetwork.cs` | Tao `TcpClient`, boc `SslStream`, gui `RELAY` preface cho LB, bat handshake TLS va doc thread TCP rieng. |
| `DrawingClient/Network/SecureTcpClient.cs` | Send/receive packet qua `SslStream`, dung length-prefix 4 byte de cat dung packet TCP. |
| `DrawingServer/Network/SecureTcpServer.cs` | Xac thuc TLS server, doc packet theo length-prefix, broadcast lai qua `SslStream` va khoa `WriteLock`. |
| `LoadBalancer/LoadBalancer.cs` | Doc/ghi `NetworkStream` theo chunk, forward 2 chieu giua client va backend, tra route response bang newline-terminated JSON. |
| `DrawingServer/Network/ClientSession.cs` | Giua mot `SslStream` va `SemaphoreSlim WriteLock` de tranh nhieu broadcast ghi xen ke. |

Muc dich cua cac stream comment trong code la de nguoi doc nhin vao file la hieu ngay: ket noi nao di qua TLS, packet nao dung length-prefix, va cho nao can lock khi ghi stream.

Con luong stream client chinh trong `DrawingClient/Network/ClientNetwork.cs` di theo thu tu: mo `TcpClient` -> boc `SslStream` -> gui `RELAY` preface neu co LoadBalancer -> gui packet length-prefix -> doc packet tu `ReceiveLoop()` -> marshal event ve UI. Phan `Send()` va `ReadExact()` la hai diem can chu y nhat khi debug loi cat/ghep packet TCP.

Hai luong stream cu the trong `LoadBalancer/LoadBalancer.cs`:

1. Luong `ROUTE`
	- Client mo `NetworkStream` va gui dong preface `ROUTE ...` de hoi LB can route ve server nao.
	- LB doc preface, co the query `owner_server_id`, roi tra ve mot dong JSON ket thuc bang newline.
	- Stream nay chi dung de dieu huong ban dau, khong mang du lieu ve canvas/chat/draw.

2. Luong `RELAY`
	- Neu client khong o che do route, LB se dung `NetworkStream` lam proxy binary 2 chieu giua client va DrawingServer.
	- `ForwardAsync()` doc/ghi tung chunk giua hai dau stream de giu duong truyen lien tuc.
	- Stream nay dung cho toan bo packet ung dung sau TLS handshake, gom login, room, chat, draw, gallery va AI.

## 4. Cac file va ham quan trong

### Client

| File | Ham/class chinh | Chuc nang |
| --- | --- | --- |
| `DrawingClient/Forms/LoginForm.cs` | `EnsureConnectedAsync`, `BtnLogin_Click`, `NetworkEvents_OnLoginResponse`, `NetworkEvents_OnRegisterResponse` | Ket noi direct/LB, gui login/register, mo Lobby khi auth thanh cong. |
| `DrawingClient/Forms/LobbyForm.cs` | UI create/join, `ReconnectToRoomOwnerViaLoadBalancerAsync`, one-shot join handler | Tao phong, join phong, route dung owner server truoc khi vao room. |
| `DrawingClient/Forms/MainForm.cs` | Event handlers draw/chat/sync/gallery/AI, `RunButtonTaskAsync`, `UIInvoke` | Man hinh chinh, toolbar, canvas, chat, members, sync board, AI, gallery. |
| `DrawingClient/Drawing/CanvasManager.cs` | Mouse handlers, render/replay action, object move/resize/delete, remote cursor | Xu ly toan bo canvas GDI+, tool ve va object layer. |
| `DrawingClient/Network/ClientNetwork.cs` | `Connect`, `ConnectRelay`, `SendLogin`, `SendRegister`, `ReceiveLoop`, `ProcessPacket`, `HeartbeatLoop` | TCP/TLS client, packet framing, heartbeat, route/reconnect, event dispatch. |
| `DrawingClient/Network/UdpManager.cs` | `Start`, `SendPacket`, `RegisterEndpoint`, `ListenLoop`, `ProcessPacket` | UDP/AES cho cursor va tin hieu tam thoi. |
| `DrawingClient/Network/LoadBalancerRouteClient.cs` | `ResolveAsync` | Goi `ROUTE`/`ROUTE room=<code>` toi LB. |
| `DrawingClient/AI/StabilityAiClient.cs` | `GenerateImageAsync` | Goi Hugging Face tao anh tu prompt. |
| `DrawingClient/AI/RemoveBgClient.cs` | `RemoveBackgroundAsync` | Goi Remove.bg xoa nen anh. |

### Server

| File | Ham/class chinh | Chuc nang |
| --- | --- | --- |
| `DrawingServer/Program.cs` | `Main` | Load env, start TCP server, UDP server, heartbeat, cross-server sync. |
| `DrawingServer/Network/SecureTcpServer.cs` | `StartAsync`, `HandleClientAsync`, `BroadcastToRoomAsync`, `SendHistoryToClientAsync`, `SaveStrokeFastPath`, `IsTurnBlocked` | Server TCP/TLS chinh, xu ly command login/room/chat/draw/gallery/AI/timeline. |
| `DrawingServer/Network/SecureUdpServer.cs` | `StartAsync`, `HandlePacketAsync`, `BroadcastUdpAsync` | UDP/AES server cho cursor/pixel/realtime tam thoi. |
| `DrawingServer/Network/ClientSession.cs` | `ClientSession`, `WriteLock` | Trang thai client tren server, khoa ghi SslStream tranh race. |
| `DrawingServer/Services/RoomService.cs` | `CreateRoomAsync`, `TryAddMemberToRoomAsync`, `RemoveMemberFromRoom`, `TryAdvanceTurn`, `GetRoomMembersInfo` | Quan ly room state trong RAM. |
| `DrawingServer/Services/Database/DbManager.cs` | `LoginAsync`, `CreateRoomAsync`, `SaveStrokeRecordAsync`, `GetRoomHistoryAsync`, `SaveChatMessageAsync`, `SaveGalleryItemAsync`, `SaveAiResultAsync`, `SaveActionStackAsync`, `SavePixelCellAsync` | Tat ca truy cap PostgreSQL. |
| `DrawingServer/Services/Database/StrokePersistenceQueue.cs` | `Enqueue`, `GetPendingStrokeJson`, `ClearRoom`, `ProcessQueueAsync` | Queue luu DrawHistory nen co retry/backoff. |
| `DrawingServer/Services/CrossServerSyncService.cs` | `Start`, `PublishEventAsync`, `ListenLoopAsync`, `OnNotification` | PostgreSQL LISTEN/NOTIFY fallback cross-server. |
| `DrawingServer/Services/ServerNodeHeartbeatService.cs` | `Start`, `HeartbeatLoopAsync`, `SendHeartbeatAsync` | Cap nhat `ServerNodes` cho monitoring/backend health. |

### SharedLib

| File | Ham/class chinh | Chuc nang |
| --- | --- | --- |
| `SharedLib/Packets/PacketDef.cs` | `CommandType`, `Packet.Serialize`, `Packet.Deserialize` | Enum command va packet framing. |
| `SharedLib/Packets/PacketHelper.cs` | `Create`, `CreateEmpty`, `GetPayload<T>`, `GetRawJson` | Tao/parse packet JSON. |
| `SharedLib/Payloads/*.cs` | Payload DTO | Contract client-server cho auth, room, draw, chat, AI, gallery, sync. |
| `SharedLib/Security/AesHelper.cs` | `Encrypt`, `Decrypt`, `TestRoundTrip` | Ma hoa/giai ma AES cho UDP. |
| `SharedLib/Security/SecurityConfig.cs` | `AesKey`, `AesIV` | Key/IV AES dung chung. |
| `SharedLib/Config/EnvLoader.cs` | `Load`, `Get`, `GetRequired`, `GetInt` | Doc `.env` va process env. |
| `SharedLib/Config/PostgresConnectionString.cs` | `Normalize` | Chuan hoa connection string PostgreSQL cho Npgsql. |
| `SharedLib/Logging/Logger.cs` | `Initialize`, `Info`, `Warning`, `Error`, `Exception`, `Debug` | Ghi log console/file. |

### LoadBalancer

| File | Ham/class chinh | Chuc nang |
| --- | --- | --- |
| `LoadBalancer/Program.cs` | `TryLoadServersFromJson`, `AddServersFromEnv` | Doc backend tu `servers.json` hoac env. |
| `LoadBalancer/LoadBalancer.cs` | `AddServer`, `StartAsync`, `HandleClientAsync`, `SelectServer`, `SelectServerForRouteAsync`, `GetRoomOwnerServerIdAsync`, `ClaimOwnerForLegacyRoomAsync`, `ForwardAsync`, `HealthCheckLoop`, `PingAsync` | TCP/UDP proxy, route theo tai, route theo room owner, health check backend. |

## 5. Da luong va bat dong bo

Du an dung ket hop `Thread`, `Task.Run`, `async/await`, `Timer`, `lock`, `SemaphoreSlim` va `BlockingCollection` de server/client khong bi treo khi nhieu nguoi ket noi, gui packet, ve realtime, goi AI hoac ghi DB.

| Muc dich | File/ham | Doan code/ky thuat xu ly |
| --- | --- | --- |
| Tach UI login khoi network blocking | `DrawingClient/Forms/LoginForm.cs` - `EnsureConnectedAsync()` | Dung `await Task.Run(() => _network.Connect(...))`, `await Task.Run(() => _network.ConnectRelay(...))` de viec ket noi TCP/TLS khong khoa UI. |
| Route LB bat dong bo | `DrawingClient/Network/LoadBalancerRouteClient.cs` - `ResolveAsync(...)` | Dung `TcpClient.ConnectAsync`, `stream.WriteAsync`, `reader.ReadLineAsync`, `Task.WhenAny(..., Task.Delay(timeoutMs))` de co timeout khi hoi `ROUTE`. |
| Thread nhan packet TCP rieng | `DrawingClient/Network/ClientNetwork.cs` - `Connect(...)`, `ConnectRelay(...)` | Tao `_receiveThread = new Thread(ReceiveLoop)` de doc packet lien tuc o background. |
| Thread heartbeat rieng | `DrawingClient/Network/ClientNetwork.cs` - `HeartbeatLoop()` | Tao `_heartbeatThread = new Thread(HeartbeatLoop)`, lap `Thread.Sleep(100)`, gui heartbeat dinh ky va phat hien timeout server. |
| Khoa ghi stream client | `DrawingClient/Network/ClientNetwork.cs` - `Send(Packet packet)` | Dung `lock (_stream)` de tranh nhieu luong ghi cung luc lam vo packet length-prefix. |
| UDP client listen background | `DrawingClient/Network/UdpManager.cs` - `Start()`, `ListenLoop(...)` | Dung `_listenTask = Task.Run(() => ListenLoop(_cts.Token))` de nhan UDP nen. |
| Timer flush cursor realtime | `DrawingClient/Forms/MainForm.cs` | Dung `System.Threading.Timer realtimePointerTimer` goi `FlushRealtimePointerState()` theo chu ky, gui latest cursor thay vi moi mouse move. |
| Marshal event ve UI thread | `LoginForm`, `LobbyForm`, `MainForm`, `GalleryForm` | Dung `BeginInvoke(...)`/`UIInvoke(...)` de packet tu network thread cap nhat WinForms an toan. |
| Khoa du lieu canvas/object | `DrawingClient/Drawing/CanvasManager.cs` | Dung `lock (textLock)`, `lock (imageLock)`, `lock (stickerLock)`, `lock (cursorLock)` khi render/update object va cursor. |
| Gioi han goi AI dong thoi | `DrawingClient/AI/StabilityAiClient.cs` | Dung `SemaphoreSlim RequestGate = new SemaphoreSlim(2, 2)` va `await RequestGate.WaitAsync(...)` de gioi han request AI cung luc. |
| HTTP AI bat dong bo | `StabilityAiClient.GenerateImageAsync`, `RemoveBgClient.RemoveBackgroundAsync` | Dung `HttpClient.SendAsync`/`PostAsync`, `ReadAsByteArrayAsync`, `CancellationToken` de goi API ngoai ma khong khoa UI. |
| Server accept nhieu TCP client | `DrawingServer/Network/SecureTcpServer.cs` - `StartAsync(...)` | Vong lap `AcceptTcpClientAsync`; moi client duoc xu ly bang `_ = Task.Run(() => HandleClientAsync(client))`. |
| Server xu ly UDP song song | `DrawingServer/Network/SecureUdpServer.cs` - `StartAsync(...)` | Sau `ReceiveAsync`, moi datagram duoc dua vao `_ = Task.Run(() => HandlePacketAsync(result))`. |
| Ghi packet server an toan | `DrawingServer/Network/ClientSession.cs`, `SecureTcpServer.SendPacketToClientAsync(...)` | Moi session co `SemaphoreSlim WriteLock`; server `await client.WriteLock.WaitAsync()` truoc khi ghi `SslStream`. |
| Luu draw history nen | `DrawingServer/Services/Database/StrokePersistenceQueue.cs` | Dung `BlockingCollection<StrokeRecord>` va `Task.Run(ProcessQueueAsync)` de luu DB nen co retry/backoff, khong chan realtime broadcast. |
| Lock room state | `DrawingServer/Services/RoomService.cs` | Dung `lock (SyncRoot)` khi them/xoa member, chuyen luot, doc room state de tranh race giua nhieu client. |
| Cross-server listener nen | `DrawingServer/Services/CrossServerSyncService.cs` | Dung `_ = Task.Run(() => ListenLoopAsync(...))`; `SemaphoreSlim PublishLock` serialize publish PostgreSQL notify. |
| Heartbeat server node nen | `DrawingServer/Services/ServerNodeHeartbeatService.cs` | Dung `_ = Task.Run(() => HeartbeatLoopAsync(...))`, dinh ky update `ServerNodes`. |
| LoadBalancer chay song song | `LoadBalancer/LoadBalancer.cs` - `StartAsync(...)` | Start `HealthCheckLoop` va `StartUdpProxyAsync` bang `Task.Run`; moi TCP client xu ly bang `Task.Run(() => HandleClientAsync(client))`. |
| Proxy TCP bat dong bo | `LoadBalancer/LoadBalancer.cs` - `ForwardAsync(...)` | Dung `ReadAsync`/`WriteAsync` cho 2 chieu client-server, `Task.WhenAny(t1, t2)` de dong proxy khi mot chieu ket thuc. |
| Health check bat dong bo | `LoadBalancer/LoadBalancer.cs` - `HealthCheckLoop()`, `PingAsync(...)` | Ping backend bang `TcpClient.ConnectAsync`, TLS handshake async va timeout bang `Task.WhenAny`. |

Vi du code tieu bieu:

```csharp
// DrawingServer/Network/SecureTcpServer.cs
TcpClient client = await _listener.AcceptTcpClientAsync();
_ = Task.Run(() => HandleClientAsync(client));
```

```csharp
// DrawingClient/Network/ClientNetwork.cs
_receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "TCP-Recv" };
_heartbeatThread = new Thread(HeartbeatLoop) { IsBackground = true, Name = "TCP-Heartbeat" };
```

```csharp
// DrawingServer/Services/Database/StrokePersistenceQueue.cs
private static readonly BlockingCollection<StrokeRecord> Queue = new BlockingCollection<StrokeRecord>();
Task.Run(ProcessQueueAsync);
```

```csharp
// DrawingServer/Network/ClientSession.cs
public SemaphoreSlim WriteLock { get; } = new SemaphoreSlim(1, 1);
```

## 6. Cryptography - ma hoa du lieu de bao mat thong tin

| Muc dich | File/ham | Mo ta |
| --- | --- | --- |
| Ma hoa UDP payload | `SharedLib/Security/AesHelper.cs` - `Encrypt(byte[] data)` | Dung AES voi key/IV trong `SecurityConfig`, ma hoa packet UDP truoc khi gui. |
| Giai ma UDP payload | `SharedLib/Security/AesHelper.cs` - `Decrypt(byte[] data)` | Giai ma du lieu UDP ma client/server nhan duoc. |
| Tu kiem tra AES | `SharedLib/Security/AesHelper.cs` - `TestRoundTrip(...)` | Encrypt roi decrypt lai chuoi test de xac nhan cau hinh AES dung. |
| Cau hinh khoa AES | `SharedLib/Security/SecurityConfig.cs` - `AesKey`, `AesIV` | Khoa/IV dung chung cho UDP/AES. |
| UDP client ma hoa khi gui | `DrawingClient/Network/UdpManager.cs` - `SendPacket(...)` | Serialize `Packet`, goi `AesHelper.Encrypt`, gui bang `UdpClient.Send`. |
| UDP client giai ma khi nhan | `DrawingClient/Network/UdpManager.cs` - `ProcessPacket(...)` | Nhan datagram UDP, goi `AesHelper.Decrypt`, parse packet va raise event. |
| UDP server giai ma khi nhan | `DrawingServer/Network/SecureUdpServer.cs` - `HandlePacketAsync(...)` | Decrypt datagram tu client, xu ly cursor/realtime/pixel. |
| UDP server ma hoa khi tra loi | `DrawingServer/Network/SecureUdpServer.cs` | Tao packet response, `AesHelper.Encrypt(packet.Serialize())`, gui ve client. |
| TLS server | `DrawingServer/Network/SecureTcpServer.cs` - `StartAsync(...)`, `HandleClientAsync(...)` | Load certificate `.pfx`, boc `TcpClient` bang `SslStream`, `AuthenticateAsServerAsync`. |
| TLS client | `DrawingClient/Network/ClientNetwork.cs` - `Connect(...)`, `ConnectRelay(...)`, `AuthenticateSslWithTimeout(...)` | Boc stream bang `SslStream`, authenticate TLS 1.2 den DrawingServer. |
| TLS health check | `LoadBalancer/LoadBalancer.cs` - `PingAsync(...)` | LB kiem tra backend bang TCP connect + TLS handshake. |
| Bam mat khau | `DrawingServer/Services/Database/DbManager.cs` - `ComputeSha256Hash(...)`, `LoginAsync(...)` | Bam password SHA-256 truoc khi so sanh/luu vao bang `Users`. |

Ket luan: du an co 3 lop bao mat chinh: TLS cho TCP, AES cho UDP, SHA-256 cho password trong DB.

Vi du code tieu bieu:

```csharp
// SharedLib/Security/AesHelper.cs
public static byte[] Encrypt(byte[] data)
public static byte[] Decrypt(byte[] data)
```

```csharp
// DrawingClient/Network/UdpManager.cs
byte[] encrypted = AesHelper.Encrypt(packet.Serialize());
byte[] decrypted = AesHelper.Decrypt(data);
```

```csharp
// DrawingServer/Network/SecureTcpServer.cs
_serverCertificate = new X509Certificate2(pfxPath, pfxPassword);
SslStream sslStream = new SslStream(tcpClient.GetStream(), false);
await sslStream.AuthenticateAsServerAsync(_serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);
```

```csharp
// DrawingServer/Services/Database/DbManager.cs
using (System.Security.Cryptography.SHA256 sha256Hash = System.Security.Cryptography.SHA256.Create())
```

## 7. Load Balancing - phan chia cong viec hop ly cho cac Server

| Muc dich | File/ham | Mo ta |
| --- | --- | --- |
| Cau hinh backend | `LoadBalancer/Program.cs` - `TryLoadServersFromJson`, `AddServersFromEnv`; `LoadBalancer/LoadBalancer.cs` - `AddServer` | Nap danh sach DrawingServer tu `servers.json` hoac env. |
| Start LB | `LoadBalancer/LoadBalancer.cs` - `StartAsync(int listenPort, int udpPort)` | Start health check, UDP proxy, TCP listener. |
| Xu ly client TCP | `LoadBalancer/LoadBalancer.cs` - `HandleClientAsync(TcpClient clientConn)` | Phan biet `ROUTE`, `RELAY`, hoac proxy default; tao ket noi sang backend va forward stream. |
| Chon server theo tai | `LoadBalancer/LoadBalancer.cs` - `SelectServer()` | Chon server healthy co `ActiveProxyConnections + RoutedClients` thap nhat. |
| Route theo server id | `LoadBalancer/LoadBalancer.cs` - `SelectServerById(...)` | Tim backend theo `server_id` khi client gui `RELAY server=<id>`. |
| Room-affinity | `LoadBalancer/LoadBalancer.cs` - `SelectServerForRouteAsync(roomCode)` | Voi room cu, doc owner server trong DB/cache de dua client vao dung DrawingServer cua phong. |
| Doc owner room tu DB | `LoadBalancer/LoadBalancer.cs` - `GetRoomOwnerServerIdAsync(...)` | Query `Rooms.owner_server_id` bang Npgsql. |
| Claim owner cho room legacy | `LoadBalancer/LoadBalancer.cs` - `ClaimOwnerForLegacyRoomAsync(...)`, `SelectServerByRoomHash(...)` | Neu room cu chua co owner, LB chon owner on dinh theo hash room va update DB. |
| Cap nhat server health | `LoadBalancer/LoadBalancer.cs` - `HealthCheckLoop()`, `PingAsync(...)`, `RefreshAndSelectServerByIdAsync(...)` | Ping TLS backend moi 5 giay, danh dau online/offline, ping lai owner stale truoc khi fail. |
| Proxy TCP 2 chieu | `LoadBalancer/LoadBalancer.cs` - `ForwardAsync(...)` | Forward byte stream giua client va backend, giu TLS end-to-end toi DrawingServer. |
| UDP proxy local/LAN | `LoadBalancer/LoadBalancer.cs` - `StartUdpProxyAsync`, `HandleUdpFromClientAsync`, `UdpProxySession` | Proxy datagram UDP tu client sang backend, dung cho local/LAN khi bat UDP proxy. |
| Client route truoc khi join | `DrawingClient/Network/LoadBalancerRouteClient.cs` - `ResolveAsync(...)`; `ClientNetwork.ReconnectToRoomOwnerViaLoadBalancerAsync(...)` | Client hoi LB `ROUTE room=<code>`, reconnect vao owner server truoc khi gui `JOIN_ROOM`. |
| Server gan owner khi tao room | `DrawingServer/Services/Database/DbManager.cs` - `CreateRoomAsync(...)` | Insert `Rooms.owner_server_id = SERVER_ID`, giup LB route dung backend ve sau. |

Ket luan: Load Balancer khong chi chia tai theo so ket noi, ma con giu room-affinity de tat ca client trong cung phong vao cung backend, tranh mat dong bo.

## 8. Database

Bang active chinh:

- `Users`: tai khoan va password hash.
- `Rooms`: metadata phong, owner user, owner server, canvas size, max members.
- `DrawHistory`: lich su draw action de replay board.
- `ChatHistory`: lich su chat.
- `Gallery`: anh da luu va public token.
- `AiResults`: ket qua AI.
- `ActionStack`: undo/redo persistent.
- `PixelArtCells`: trang thai pixel art.
- `ServerNodes`: heartbeat backend server.
- `RoomEvents`: fallback cross-server sync.

DB duoc truy cap chu yeu qua `DrawingServer/Services/Database/DbManager.cs`. LoadBalancer cung doc/update `Rooms.owner_server_id` de route phong.

## 9. Setup nhanh

Copy env:

```powershell
Copy-Item .\.env.example .\.env
Copy-Item .\setup\.env.example .\setup\.env
```

Dien cac bien quan trong:

```env
DATABASE_URL=postgresql://...
SERVER_CERT_PATH=...
SERVER_CERT_PASSWORD=...
HF_TOKEN=...
HF_IMAGE_MODEL=stabilityai/stable-diffusion-xl-base-1.0
REMOVE_BG_API_KEY=...
MAX_ROOM_MEMBERS=5
```

Restore/build:

```powershell
dotnet restore .\NT106_DrawingApp.sln /p:RestorePackagesConfig=true
dotnet build .\NT106_DrawingApp.sln -v:minimal
```

Test:

```powershell
dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal
```

## 10. Cac kich ban chay demo

### 1 server local, khong LoadBalancer

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-local-no-lb.ps1 -StopExisting
```

Smoke server, khong mo client:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-local-no-lb.ps1 -ClientCount 0 -StopExisting
```

### 2 server + LoadBalancer local

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-local-with-lb.ps1 -StopExisting
```

### LAN direct

Server:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-server.ps1 -ServerId server-1 -StopExisting
```

Client:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-client.ps1 -Mode Direct -Host <server-lan-ip>
```

### Internet qua ngrok + LoadBalancer

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-local-with-lb.ps1 -ClientCount 0 -StartNgrok -StopExisting
```

Client Internet:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-client.ps1 -Mode LbRelay -InternetNgrok -Host <ngrok-host> -TcpPort <ngrok-port>
```

### Internet direct server, khong LoadBalancer

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-server.ps1 -ServerId server-1 -StartNgrok -StopExisting
powershell -ExecutionPolicy Bypass -File .\setup\start-client.ps1 -Mode Direct -InternetNgrok -Host <ngrok-host> -TcpPort <ngrok-port>
```

## 11. Goi release

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\package-release.ps1
```

Lenh nay build Release, copy exe/DLL vao `setup/apps`, va tao lai `NT106-DrawingApp-setup.zip`.

## 12. Ghi chu trang thai

- Draw/fill/text quan trong dang di TCP reliable.
- UDP chu yeu dung cho cursor/tin hieu tam thoi trong local/LAN.
- Internet ngrok chi public TCP, client tu dung TCP fallback.
- Cross-server sync bang PostgreSQL LISTEN/NOTIFY chi la fallback; room-affinity cua LoadBalancer moi la duong chinh.
- Mot so tinh nang da bi loai khoi scope hien tai: GIF export, snapshot, claim area/khoa vung ve, reaction, sticker rotate UI, sticky note replies.
- Mot so tinh nang con backlog/can chot neu muon hoan thien: spectator chi xem/chat, pixel art UI 64x64 tich hop canvas, follow viewport day du.
