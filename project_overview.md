# Tong quan du an NT106 Drawing App

Trang thai tai lieu: tong hop tu `local/description.md`, `local/features.md`, cac file `.md` trong `setup/`, va source code hien tai.

## 1. Muc tieu va kien truc tong the

NT106 Drawing App la ung dung ve cong tac realtime bang WinForms, target `.NET Framework 4.7.2`. He thong gom 4 project chinh:

- `DrawingClient`: ung dung WinForms cho nguoi dung dang nhap/dang ky, tao/join phong, ve tren canvas, chat, gallery, AI image, import/sticker/text/sticky note.
- `DrawingServer`: server TCP/TLS va UDP/AES, xu ly packet, quan ly phong, broadcast realtime, ghi PostgreSQL.
- `LoadBalancer`: ingress TCP/UDP cho demo local/LAN/Internet qua ngrok, route client toi server backend theo room-affinity.
- `SharedLib`: thu vien dung chung cho packet, payload DTO, AES, logger, env loader, API config.

Topology demo da chot:

1. Client ket noi truc tiep server trong local/LAN hoac di qua LoadBalancer.
2. Khi dung Internet/ngrok, client chi public TCP vao LoadBalancer; UDP khong dung qua ngrok, cursor/tin hieu tam thoi dung TCP fallback.
3. LoadBalancer route phong cu theo `Rooms.owner_server_id` de tranh cung mot room bi chia sang 2 server.
4. DrawingServer broadcast net ve/chat/realtime cho client trong room truoc, sau do moi luu DB nen neu la draw action tan suat cao.
5. PostgreSQL/DigitalOcean luu users, rooms, draw history, chat, gallery, AI results, pixel art, server node heartbeat, cross-server events, undo/redo stack.

## 2. Cau truc thu muc

| Thu muc/file | Vai tro |
| --- | --- |
| `DrawingClient/` | Ma nguon WinForms client. Chua forms, network client, canvas engine, AI client, UI helper. |
| `DrawingServer/` | Ma nguon server TCP/TLS + UDP/AES, room service, DB manager, migrations. |
| `LoadBalancer/` | TCP relay, ROUTE/RELAY protocol, UDP proxy local/LAN, health check backend, room-affinity DB lookup. |
| `SharedLib/` | Contract dung chung: command enum, packet framing, payloads, AES, env loader, logger, API config. |
| `NT106Tests/` | Unit/load/security tests cho AES, packet, env loader, connection string, load/performance. |
| `setup/` | Goi trien khai demo: scripts PowerShell, binary release trong `setup/apps`, README/checklist. |
| `local/` | Tai lieu noi bo, plan/done/status, file tong quan du an, helper tam thoi. |
| `.env`, `.env.example`, `setup/.env.example` | Cau hinh runtime. `.env` that khong commit, `.env.example` la mau. |
| `NT106_DrawingApp.sln` | Solution chinh. |
| `NT106-DrawingApp-setup.zip` | Goi release da dong de demo. |

Luu y: cac thu muc `bin`, `obj`, `local/tmp_build`, `setup/apps`, `*/setup/apps` la output build/runtime package, khong phai source logic chinh.

## 3. Tai lieu setup va kich ban trien khai

`setup/README.md` dinh nghia cac kich ban:

- Kich ban 1: 1 may, 1 server, khong LoadBalancer. Lenh chinh: `start-local-no-lb.ps1 -StopExisting`.
- Kich ban 2: 1 may, 2 server + 1 LoadBalancer + client. Lenh chinh: `start-local-with-lb.ps1 -StopExisting`.
- Kich ban 3: LAN direct, client ket noi thang server bang IP LAN.
- Kich ban 4: LAN co LoadBalancer, LB hoi IP server-1/server-2, client vao LB.
- Kich ban 5.1: Internet ngrok, LB va 2 server cung may.
- Kich ban 5.2: Internet ngrok, LB rieng, server cung LAN.
- Kich ban 5.3: Internet ngrok, LB va server khac mang, dung Tailscale cho duong LB -> server.
- Kich ban 5.4: Internet direct server, khong LoadBalancer.

`setup/CHECKLIST.md` la checklist demo: chuan bi `.env`, kiem tra exe ton tai, smoke local, LAN, Internet ngrok, muc tieu 3 client + 2 server + 1 LB, draw/chat/history pass.

Scripts chinh:

| File | Chuc nang |
| --- | --- |
| `setup/_common.ps1` | Helper chung cho env/process/path/wait port/TLS. |
| `setup/start-local-no-lb.ps1` | Start 1 server local, ep client direct vao `127.0.0.1:8888`. |
| `setup/start-local-with-lb.ps1` | Start 2 server + LB local, tuy chon start ngrok. |
| `setup/start-server.ps1` | Start 1 DrawingServer theo `ServerId`, port tu env/mac dinh. |
| `setup/start-load-balancer.ps1` | Start LB, hoi host/IP backend, co the start ngrok TCP. |
| `setup/start-client.ps1` | Start client o mode Direct/LbRelay/LAN/InternetNgrok. |
| `setup/package-release.ps1` | Build Release, copy dependency vao `setup/apps`, tao zip setup. |

## 4. SharedLib

| File | Ham/class chinh | Chuc nang |
| --- | --- | --- |
| `SharedLib/Packets/PacketDef.cs` | `CommandType`, `Packet.Serialize`, `Packet.Deserialize` | Dinh nghia ma lenh giao thuc va serialize/deserialize packet voi payload byte. |
| `SharedLib/Packets/PacketHelper.cs` | `Create`, `CreateEmpty`, `GetPayload<T>`, `GetRawJson` | Tao packet tu object payload, parse payload JSON ve DTO. |
| `SharedLib/Payloads/AuthPayload.cs` | `LoginPayload`, `LoginResponse`, `RegisterPayload`, `RegisterResponse` | DTO auth client-server. |
| `SharedLib/Payloads/RoomPayload.cs` | `CreateRoomPayload`, `JoinRoomPayload`, `RoomMembersPayload`, ... | DTO tao/join room, canvas size, members. |
| `SharedLib/Payloads/DrawPayload.cs` | `DrawPayload`, `FloodFillPayload`, `ImportImagePayload`, `SetBackgroundPayload` | DTO net ve, flood fill, text, image import, background. |
| `SharedLib/Payloads/InteractionPayload.cs` | `CursorPayload`, `ChatPayload`, `StickerPayload`, `StickyNotePayload`, `TurnBasedPayload`, ... | DTO cursor/chat/sticker/sticky note/follow/turn-based. Mot so DTO legacy con ton tai du tinh nang da bo UI. |
| `SharedLib/Payloads/SyncPayload.cs` | `SyncBoardPayload`, `DrawAction`, `UndoPayload`, `RedoPayload`, timeline/playback payload | DTO dong bo board, chunk history, undo/redo, timeline. |
| `SharedLib/Payloads/GalleryPayload.cs` | `SaveGalleryPayload`, `GalleryResponsePayload`, `GalleryItem`, `PublicGalleryLinkPayload` | DTO gallery. |
| `SharedLib/Payloads/AiPayload.cs` | `AiTextToImageRequestPayload`, `AiTextToImageResultPayload`, `AiBgRemovedPayload` | DTO AI image/remove background. |
| `SharedLib/Payloads/PixelArtPayload.cs` | `PixelArtDrawPayload`, `PixelArtSyncPayload`, `PixelCell` | DTO pixel art. |
| `SharedLib/Security/AesHelper.cs` | `Encrypt`, `Decrypt`, `TestRoundTrip` | Ma hoa/giai ma AES cho UDP payload. |
| `SharedLib/Security/SecurityConfig.cs` | key/IV config | Cau hinh AES. |
| `SharedLib/Config/EnvLoader.cs` | `Load`, `Get`, `GetRequired`, `GetInt`, `IsConfigured` | Doc `.env`, process env uu tien hon file env. |
| `SharedLib/Config/PostgresConnectionString.cs` | `Normalize` va cac ham normalize URI/key-value | Chuan hoa PostgreSQL URI DigitalOcean/Neon thanh connection string Npgsql hop le. |
| `SharedLib/Logging/Logger.cs` | `Initialize`, `Info`, `Warning`, `Error`, `Exception`, `Debug`, `Close` | Ghi log console va file `logs`. |
| `SharedLib/AI/ApiConfig.cs` | `IsHuggingFaceConfigured`, `IsRemoveBgConfigured` | Doc token/model AI tu env. |

## 5. DrawingClient

### Forms/UI

| File | Ham/chuc nang chinh |
| --- | --- |
| `DrawingClient/Program.cs` | Entry point WinForms, load env/logger, bat `Application.ThreadException` va unhandled exception de log crash client. |
| `DrawingClient/Forms/LoginForm.cs` | `BuildUi` tao form login; `BtnLogin_Click` validate input va gui login; nut dang ky gui register; `EnsureConnectedAsync` chon direct/LB/relay, route server va connect async; `NetworkEvents_OnLoginResponse` mo `LobbyForm` khi login thanh cong; `NetworkEvents_OnRegisterResponse` cap nhat trang thai. |
| `DrawingClient/Forms/LobbyForm.cs` | `BuildUi` tao UI tao/join room; nut create gui `CREATE_ROOM`; nut join goi `ReconnectToRoomOwnerViaLoadBalancerAsync`, tao `MainForm` truoc khi gui `JOIN_ROOM` de tranh miss `SYNC_BOARD`; handlers create/join marshal ve UI thread. |
| `DrawingClient/Forms/MainForm.cs` | UI chinh: toolbar, canvas, chat/members/logs, gallery/AI/sticker; bind event tu `CanvasManager`; gui draw/chat/cursor/undo/redo/background/gallery/AI; nhan packet realtime qua `NetworkEvents`; gom chunk `SYNC_BOARD`; quan ly action history client-side. |
| `DrawingClient/Forms/GalleryForm.cs` | Hien gallery, nhan `GALLERY_RESPONSE`, decode image base64, save anh local bang `SaveFileDialog`. |
| `DrawingClient/UI/ToastForm.cs` | `ShowToast` hien thong bao noi ngan. |
| `DrawingClient/UI/CursorLayer.cs` | Overlay emoji/reaction legacy; cursor realtime hien duoc render trong `CanvasManager`/`MainForm` moi. |
| `DrawingClient/Form1.cs` | Form test/legacy. Khong phai flow chinh. |

### Drawing engine

| File | Ham/chuc nang chinh |
| --- | --- |
| `DrawingClient/Drawing/CanvasManager.cs` | Quan ly bitmap canvas 1920x1080, render GDI+, pan/zoom local, mouse down/move/up, pen/line/rectangle/circle/eraser/pipette/flood fill/text/import/sticker/background; object selection/move/resize/delete; remote cursor; replay `DrawAction`; export canvas thanh image/base64. Cac callback `OnNetwork*Action` day payload len `MainForm` de gui server. |
| `DrawingClient/Drawing/DrawingTools.cs` | Enum/constant tool ve. |
| `DrawingClient/Drawing/FloodFill.cs` | BFS flood fill tren bitmap. |
| `DrawingClient/Drawing/TextTool.cs` | Tao TextBox editor tai viewport, commit text ve toa do canvas. |
| `DrawingClient/Drawing/UndoStack.cs` | Stack undo/redo local/legacy; undo/redo hien chinh nam trong `MainForm` theo `ActionID`/`Username`. |

### Network client

| File | Ham/chuc nang chinh |
| --- | --- |
| `DrawingClient/Network/ClientNetwork.cs` | `Connect` ket noi TCP/TLS direct; `ConnectRelay` gui preface `RELAY server=...` qua LB roi TLS; `ReconnectToRoomOwnerViaLoadBalancerAsync` resolve owner room va login lai nen; `HeartbeatLoop` giu ket noi; `ReceiveLoop` doc packet length-prefix; `ProcessPacket` raise event; `Send*` dong goi command auth/room/draw/chat/gallery/realtime. |
| `DrawingClient/Network/LoadBalancerRouteClient.cs` | `ResolveAsync` gui `ROUTE` hoac `ROUTE room=<roomCode>` toi LB, doc JSON route/error. |
| `DrawingClient/Network/NetworkEvents.cs` | Event hub static cho cac packet nhan duoc; `SafeInvoke` boc try/catch tung subscriber. |
| `DrawingClient/Network/UdpManager.cs` | Mot `UdpClient` cho ca send/receive UDP AES, register endpoint, send cursor/pixel/realtime tam thoi, listen loop va process packet. |
| `DrawingClient/Network/SecureTcpClient.cs` | Wrapper TCP/TLS async co `ConnectAsync`, `SendAsync`, `ReceiveAsync`; hien la helper/legacy so voi `ClientNetwork`. |
| `DrawingClient/Network/SecureUdpSender.cs`, `SecureUdpReceiver.cs` | Helper UDP AES cu cho send/receive rieng. |

### AI client

| File | Ham/chuc nang chinh |
| --- | --- |
| `DrawingClient/AI/StabilityAiClient.cs` | `GenerateImageAsync` goi Hugging Face Routing image generation, parse base64 image; `RequestGate` gioi han concurrency. |
| `DrawingClient/AI/RemoveBgClient.cs` | Goi remove.bg API, nhan PNG da xoa nen, validate image. |
| `DrawingClient/AI/VoiceClient.cs` | Voice recognition helper, khong phai flow chinh theo tai lieu hien tai. |

## 6. DrawingServer

| File | Ham/chuc nang chinh |
| --- | --- |
| `DrawingServer/Program.cs` | Load env/logger, doc port/cert, start `SecureTcpServer`, `SecureUdpServer`, `ServerNodeHeartbeatService`, `CrossServerSyncService`. |
| `DrawingServer/Network/SecureTcpServer.cs` | `StartAsync` listen TCP/TLS; `HandleClientAsync` authenticate TLS, doc packet, switch command; login/register/room/chat/draw/gallery/AI/pixel/timeline; `BroadcastToRoomAsync` broadcast room; `SendHistoryToClientAsync` chunk `SYNC_BOARD`; `SaveStrokeFastPath` enqueue DB save; `IsTurnBlocked` enforce turn-based. |
| `DrawingServer/Network/SecureUdpServer.cs` | `StartAsync` listen UDP; `HandlePacketAsync` decrypt AES, xu ly endpoint registration/cursor/pixel/realtime; broadcast UDP cho client co endpoint, TCP fallback khi can. |
| `DrawingServer/Network/ClientSession.cs` | Trang thai ket noi cua client: TCP, `SslStream`, username, room, UDP endpoint, `WriteLock`, disconnected flag. |
| `DrawingServer/Services/RoomService.cs` | `CreateRoomAsync`, `TryAddMemberToRoomAsync`, `RemoveMemberFromRoom`, `TryAdvanceTurn`, `TryAdvanceTurnAfterMemberRemoval`, `GetRoomMembersInfo`; quan ly room state trong RAM voi lock. |
| `DrawingServer/Services/Database/DbManager.cs` | Tat ca truy cap PostgreSQL: auth, rooms, draw history, chat, gallery, AI, undo/redo action stack, pixel art, timeline, server nodes. |
| `DrawingServer/Services/Database/StrokePersistenceQueue.cs` | Queue nen cho draw history: `Enqueue`, `GetPendingStrokeJson`, `ClearRoom`, worker `ProcessQueueAsync` retry/backoff. |
| `DrawingServer/Services/CrossServerSyncService.cs` | PostgreSQL `LISTEN/NOTIFY` fallback cross-server; `PublishEventAsync`, `ListenLoopAsync`, `OnNotification`. |
| `DrawingServer/Services/ServerNodeHeartbeatService.cs` | Background heartbeat update `ServerNodes`. |
| `DrawingServer/Services/AuthService.cs` | Session online trong RAM, user color, logout, room user lookup. Co `AuthenticateUserAsync` nhung flow TCP hien dung truc tiep `DbManager.LoginAsync`. |
| `DrawingServer/Services/DrawService.cs` | Service luu draw/text/fill/import/background/clear/get history; nhieu flow hien da di qua `SecureTcpServer` + `StrokePersistenceQueue`, nen file nay mang tinh service helper/legacy. |
| `DrawingServer/Server.cs`, `ClientHandler.cs`, `UdpServer.cs` | Server TCP/UDP khong TLS/legacy/test, khong phai flow demo chinh. |
| `DrawingServer/Services/Database/Migrations/*.sql` | Schema migrations 001-005: base schema, collaboration, AI/routing, drop unused tables, drop snapshots. |
| `DrawingServer/database_setup.sql` | Fresh schema setup. |

## 7. LoadBalancer

| File | Ham/chuc nang chinh |
| --- | --- |
| `LoadBalancer/Program.cs` | Load env, doc `servers.json` hoac env `LB_SERVER_*`, tao `DrawingLoadBalancer`, start port TCP/UDP. |
| `LoadBalancer/LoadBalancer.cs` | `AddServer`; `StartAsync`; `StartUdpProxyAsync`; `HandleClientAsync` xu ly `ROUTE`, `RELAY` va proxy stream; `SelectServerForRouteAsync` room-affinity; `GetRoomOwnerServerIdAsync` doc DB; `ClaimOwnerForLegacyRoomAsync` gan owner cho room cu; `ForwardAsync` relay bytes; `HealthCheckLoop` ping TLS backend; `UdpProxySession` proxy UDP local/LAN. |
| `LoadBalancer/servers.example.json` | Mau danh sach backend. `setup/apps/LoadBalancer/servers.json` la ban runtime. |

## 8. NT106Tests

| File | Chuc nang |
| --- | --- |
| `SecurityTests.cs` | Test AES round-trip, packet serialize, logger. |
| `EnvLoaderTests.cs` | Test doc `.env`, uu tien env process, required key. |
| `PostgresConnectionStringTests.cs` | Test normalize URI/key-value PostgreSQL, sslmode/channel binding/max pool. |
| `LoadTests.cs` | Test serialize/encrypt performance, large image packet, concurrent TCP connection test (co skip neu server khong chay). |

## 9. Luong hoat dong chinh

### Dang nhap/dang ky

1. `LoginForm.BtnLogin_Click` validate username/password.
2. `LoginForm.EnsureConnectedAsync` ket noi server:
   - Direct: `ClientNetwork.Connect`.
   - LB direct route: `LoadBalancerRouteClient.ResolveAsync`, roi connect server duoc route.
   - LB relay: resolve server id, `ClientNetwork.ConnectRelay`.
3. `ClientNetwork.SendLogin` gui `LOGIN`; `SendRegister` gui `REGISTER`.
4. `SecureTcpServer.HandleClientAsync` nhan `LOGIN`/`REGISTER`, goi `DbManager.LoginAsync`.
5. `DbManager.LoginAsync` hash SHA256 password, query `Users`, neu user chua co thi auto-register, neu co thi so password hash.
6. Server tra `LOGIN_RESPONSE`/`REGISTER_RESPONSE`.
7. `ClientNetwork.ProcessPacket` raise event; `LoginForm.NetworkEvents_OnLoginResponse` mo `LobbyForm`.
8. Trang thai client sau login nam trong `ClientNetwork.CurrentUsername`, `_lastPassword` dung de reconnect room-owner qua LB.

### Tao va tham gia phong

1. `LobbyForm` nut tao phong goi `ClientNetwork.SendCreateRoom`.
2. Server `CREATE_ROOM` goi `RoomService.CreateRoomAsync`.
3. `RoomService` goi `DbManager.CreateRoomAsync` insert `Rooms` voi `owner_server_id=SERVER_ID`, tao `RoomState` trong RAM.
4. Client nhan `CREATE_ROOM_RESPONSE`, tao `MainForm`, subscribe events, gui `JOIN_ROOM`.
5. Join phong thu cong: `LobbyForm` goi `ClientNetwork.ReconnectToRoomOwnerViaLoadBalancerAsync(roomCode)` truoc, neu can thi resolve `ROUTE room=...` va reconnect vao owner server.
6. Server `JOIN_ROOM` check DB room, check max members, goi `RoomService.TryAddMemberToRoomAsync`, gui members/chat history/draw history/action stack.
7. `SendHistoryToClientAsync` chia history thanh chunk `SYNC_BOARD`; `MainForm` gom chunk va render khi chunk cuoi den.

### Ve realtime va sync board

1. User thao tac tren `CanvasManager`.
2. `CanvasManager` tao payload va goi callback `OnNetworkDrawAction`, `OnNetworkFloodFillAction`, `OnNetworkTextAction`, `OnNetworkImportImageAction`, `OnNetworkStickerAction`.
3. `MainForm` nhan callback va goi `ClientNetwork.SendDrawRealtime`/`SendFloodFillRealtime`/`SendTextRealtime`/`SendSticker`/`Send`.
4. Server `SecureTcpServer` nhan command draw/text/fill/spray/import/sticker/background.
5. Server gan `Username`, `ToolType`, `ActionID`, broadcast ngay cho room, roi `SaveStrokeFastPath` dua vao `StrokePersistenceQueue`.
6. `StrokePersistenceQueue.ProcessQueueAsync` luu `DrawHistory` bang `DbManager.SaveStrokeRecordAsync`; neu DB loi/het slot thi retry backoff va giu pending trong RAM.
7. Client join sau goi `DbManager.GetRoomHistoryAsync`, ham nay doc DB history va ghep them pending stroke chua flush.

### Chat

1. `MainForm` gui chat qua `ClientNetwork.SendChat`.
2. Server `CHAT` gan username tu session, luu `ChatHistory` bang `DbManager.SaveChatMessageAsync`, broadcast room.
3. Client nhan `CHAT`, `MainForm` append vao RichTextBox chat co word-wrap.
4. Khi join, server doc 50 tin gan nhat bang `DbManager.GetChatMessagesAsync` va gui lai.

### Gallery/export

1. Export local: `MainForm`/`CanvasManager` tao image, `SaveFileDialog` ghi file tren may client.
2. Save gallery: client convert canvas/image thanh base64, gui `SAVE_TO_GALLERY`.
3. Server `DbManager.SaveGalleryItemAsync` insert `Gallery`, tra `SaveGalleryResponse`, broadcast public link.
4. `GET_GALLERY` doc danh sach bang `DbManager.GetGalleryItemsAsync`, client hien trong `GalleryForm`.

### AI image/remove background

1. Text-to-image: `MainForm` goi `StabilityAiClient.GenerateImageAsync`, client nhan image bytes, chen vao canvas nhu import image, gui packet `AI_TEXT_TO_IMAGE`.
2. Remove background: `RemoveBgClient` goi remove.bg; neu dang chon image object thi cap nhat object cung `ActionID`, neu khong thi chen image moi.
3. Server broadcast packet va `PersistAiImageInBackground` luu `AiResults` + `DrawHistory`.

### LoadBalancer relay/room-affinity

1. Client can route goi `LoadBalancerRouteClient.ResolveAsync`.
2. LB `HandleClientAsync` neu thay `ROUTE` thi goi `SelectServerForRouteAsync`.
3. Room moi/chua co roomCode: LB chon server healthy it tai.
4. Room cu: LB doc `Rooms.owner_server_id`; neu owner unhealthy thi ping lai; neu room legacy owner rong thi hash room va update DB; neu khong route duoc thi tra error.
5. Client relay gui `RELAY server=<serverId>` truoc TLS, LB proxy raw stream sang backend dung.

## 10. Code dap ung cac tieu chi I/O, Database, Thread, Sign up/Sign in

| Tieu chi | Doan code/file | Giai thich |
| --- | --- | --- |
| I/O File | `SharedLib/Config/EnvLoader.cs` (`File.Exists`, `File.ReadAllLines`), `SharedLib/Logging/Logger.cs` (`Directory.CreateDirectory`, `StreamWriter.WriteLine`), `DrawingClient/Forms/MainForm.cs` (`OpenFileDialog`, `SaveFileDialog`, `File.ReadAllBytes`, `MemoryStream`), `DrawingClient/Forms/GalleryForm.cs` (`File.WriteAllBytes`) | Doc env tu file, ghi log, doc anh local, xuat/luu anh. |
| I/O Network TCP/TLS | `DrawingClient/Network/ClientNetwork.cs:53`, `:97`, `:335`; `DrawingServer/Network/SecureTcpServer.cs:31`, `:54`; `LoadBalancer/LoadBalancer.cs:167`, `:564` | Client/server giao tiep length-prefix qua TCP/TLS; LB proxy stream 2 chieu. |
| I/O Network UDP/AES | `DrawingClient/Network/UdpManager.cs`, `DrawingServer/Network/SecureUdpServer.cs`, `LoadBalancer/LoadBalancer.cs:82` | Cursor/endpoint/pixel art realtime local/LAN qua UDP ma hoa AES, co proxy UDP qua LB khi bat. |
| I/O HTTP API | `DrawingClient/AI/StabilityAiClient.cs`, `DrawingClient/AI/RemoveBgClient.cs` | Goi Hugging Face va remove.bg bang `HttpClient`. |
| Database | `DrawingServer/Services/Database/DbManager.cs:70`, `:103`, `:185`, `:217`, `:366`, `:506`, `:568`, `:640`, `:733`, `:829`; `LoadBalancer/LoadBalancer.cs:367`, `:394` | Npgsql ket noi PostgreSQL, CRUD users/rooms/history/chat/gallery/AI/action stack/pixel/timeline; LB doc/update owner server room. |
| Thread/Da luong | `DrawingClient/Network/ClientNetwork.cs:79`, `:82`, `:230`, `:335`; `DrawingServer/Network/SecureTcpServer.cs:45`; `DrawingServer/Network/SecureUdpServer.cs:32`; `LoadBalancer/LoadBalancer.cs:68`, `:78`, `:92`; `StrokePersistenceQueue.cs:22`, `:98`; `MainForm.cs:368` | Client co thread receive va heartbeat; server/LB spawn task moi cho client/UDP packet; queue nen DB; timer flush cursor; lock/semaphore tranh race. |
| Sign up/Sign in | `DrawingClient/Forms/LoginForm.cs:128`, `:154`, `:236`, `:261`; `DrawingClient/Network/ClientNetwork.cs:303`, `:309`; `DrawingServer/Network/SecureTcpServer.cs:89`, `:100`; `DrawingServer/Services/Database/DbManager.cs:70` | UI login/register, ket noi server, gui packet auth, server validate/hash/auto-register trong DB, tra response va client luu state username/password de reconnect. |
| Luu trang thai dang nhap | `ClientNetwork.CurrentUsername`, `_lastPassword`, `CurrentRoomCode`; `DrawingServer/Network/ClientSession.cs`; `DrawingServer/Services/AuthService.cs` | Client giu username/room/password reconnect; server session giu username, room, UDP endpoint; `AuthService` giu session online/color trong RAM. |

## 11. Diem chua chac/nen chot them

1. `Register` hien tai tren server dung `DbManager.LoginAsync` co co che auto-register. Nghia la `LOGIN` voi user moi cung co the tao tai khoan. Neu bao cao yeu cau dang ky/dang nhap tach logic nghiem ngat, can chot co can sua flow nay khong.
2. `Pixel art 64x64 tich hop canvas` trong tai lieu dang ghi chua hoan thien UI user-facing; server/DB payload da co nhung server `PIXEL_ART_SYNC` dang tra `GridSize = 32`. Can chot scope neu day la tinh nang bat buoc.
3. `Che do quan sat` va `follow viewport` con trang thai chua hoan thien/legacy theo `features.md`. Can chot co dua vao bao cao nhu backlog hay can implement tiep.
4. Mot so file legacy (`Server.cs`, `ClientHandler.cs`, `UdpServer.cs`, `SecureTcpClient.cs`, `SecureUdpSender/Receiver.cs`, `DrawService.cs`) khong phai flow demo chinh. Neu giao vien yeu cau liet ke "tat ca file source" thi co the them phu luc chi tiet hon cho cac file legacy/auto-generated.
