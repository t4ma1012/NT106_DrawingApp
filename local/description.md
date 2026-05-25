# NT106 Drawing App - tri thuc tong quan du an

Trang thai: `ACTIVE`

File nay la entrypoint boi canh cho cac phien lam viec sau. Neu can nam du an nhanh, doc file nay truoc; cac file chi tiet nam trong `local/plan`, `local/done`, `local/setup` va code theo duong dan ben duoi.

## Muc tieu va kien truc

Ung dung la collaborative drawing app bang WinForms, target `.NET Framework 4.7.2`, co 4 project chinh:

- `DrawingClient/`: app WinForms cho nguoi dung ve, chat, AI, gallery, lobby/login.
- `DrawingServer/`: server TCP/TLS + UDP/AES, quan ly room, broadcast realtime, luu Neon/PostgreSQL.
- `LoadBalancer/`: public ingress cho demo internet/ngrok; proxy TCP relay den 2 drawing server.
- `SharedLib/`: packet, payload, security, logging, env loader dung chung.

Demo ha tang da chot:

- 3 client internet -> ngrok TCP -> `LoadBalancer` -> 2 `DrawingServer` -> Neon.
- LB la public ingress duy nhat; drawing server khong expose truc tiep internet.
- LB -> server dung LAN neu cung mang, hoac Tailscale/MagicDNS neu khac mang.
- Client demo khong can cai Tailscale.
- Room limit mac dinh: `MAX_ROOM_MEMBERS=5`.

## Trang thai moi nhat

- Build/test gan nhat 2026-05-25 sau khi them `role-setup/`, don schema Neon va sua direct UDP port cho playit: `dotnet build .\NT106_DrawingApp.sln -v:minimal -p:OutputPath=..\local\tmp_build\Solution\` pass 0 warning; `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal -p:OutputPath=..\local\tmp_build\Tests\` pass 17, skip 1 (`TcpConcurrentConnections_10Clients` can server dang chay), total 18. Build output mac dinh co the bi khoa neu dang chay `DrawingClient`/`LoadBalancer`/`DrawingServer`; khi app dang mo nen build vao output tam trong `local\tmp_build`.
- Cap nhat 2026-05-25: da audit Neon cloud bang helper Npgsql. Truoc khi don, schema co cac bang rong/khong con code dung: `clientratelimits`, `gifexports`, `roommembers`, `stickers`, `stickynotes`, `stickynotereplies`. Da them va apply migration `004_drop_unused_tables.sql`; schema cloud con cac bang active: `users`, `rooms`, `drawhistory`, `chathistory`, `gallery`, `actionstack`, `airesults`, `snapshots`, `pixelartcells`, `servernodes`, `roomevents`.
- Cap nhat 2026-05-25: da them thu muc `role-setup/` de moi role chay script rieng: `start-server.ps1`, `start-load-balancer.ps1`, `start-client.ps1`, `start-local-all.ps1`, kem `README.md`. Thu muc nay la goi setup cho client/LB/server tai ve va chay theo kich ban. `LoadBalancer/servers.json` la file runtime generated va da dua vao `.gitignore`; dung `servers.example.json` hoac script de tao lai.
- Cap nhat 2026-05-25: da bo sung `local/plan/12_kich_ban_demo_playit.md` va `local/plan/13_cau_hoi_dua_du_an_vao_thuc_te.md`. Kich ban playit tach ro internet direct TCP+UDP (giu dung UDP cho cursor/laser) voi internet qua LoadBalancer relay (TCP-only relay, cursor/laser fallback TCP vi LB hien chua proxy UDP).
- Cap nhat 2026-05-25: client direct mode da doc `SERVER_UDP_PORT` truoc khi connect, nen khi playit cap public TCP/UDP port khac nhau co the chay `start-client.ps1 -Mode Direct -Host <playit-host> -TcpPort <tcp> -UdpPort <udp>` de UDP/AES cursor/laser di dung endpoint.
- Canvas da chot co dinh 1920x1080 tren client va room; canvas nay tu cover vung nhin thay o muc zoom-out toi da (khong lo khoang chet), khong con combobox chon kich thuoc trong sidebar. Zoom la local, dung `Ctrl + MouseWheel`, `Ctrl +` va `Ctrl -`; co tool `Chuột` de pan viewport bang cach giu chuot trai keo, va khung ben phai co the an/hien tu thanh cong cu trai de mo rong vung ve.
- User da test va xac nhan tot luong canvas viewport moi: zoom-out toi da van ve duoc o moi diem dang thay, va nen mau/anh duoc phu dung theo viewport.
- Test gan nhat 2026-05-25: `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal -p:OutputPath=..\local\tmp_build\Tests\` pass 17, skip 1 (`TcpConcurrentConnections_10Clients` can server dang chay), total 18. Lenh test mac dinh bi khoa neu `DrawingClient.exe` dang mo.
- Da xoa cac tinh nang game/challenge da bo: scoring/leaderboard, drawing prompt, blind draw va vote contract; giu lai pixel art nhu tinh nang to mau cong tac theo tung pixel.
- `LoadBalancer` da chuyen sang SDK-style `PackageReference`, dung `Npgsql 8.0.9` giong `DrawingServer`.
- Realtime drawing da chot dung TCP cho draw/flood fill/text o ca relay va direct/LAN de tranh mat net do UDP drop. UDP van la duong nhanh cho cursor/laser/ping/pixel-art trong direct/LAN, nhung cursor/laser da co TCP fallback cho LB relay/ngrok hoac client chua co UDP endpoint. Reaction da duoc user loai khoi scope. Server TCP draw/flood fill/text/spray broadcast ngay, luu `DrawHistory` nen sau, de Neon khong nam tren critical path cua net ve.
- Da bo sung chon anh lam nen canvas: client chon file local, nen anh duoc nen/rescale ve kich thuoc canvas, gui bang `SET_BACKGROUND` voi `SetBackgroundPayload.ImageData`, server luu/replay trong `DrawHistory` nhu `ToolType=SetBackground`.
- Da sua loi TextBox tool: toa do text duoc luu theo he canvas (khong con mat/lech chu khi zoom-pan), va text duoc render theo object layer de co the keo-tha/chinh co sau khi dat.
- Da bo sung object manipulation tren client (tool `Mouse`): image import, sticker, textbox text va sticky note deu co the keo-tha; image/sticker/sticky note resize duoc, text doi co chu. Dong bo/replay dua tren `ActionID` de update dung object thay vi tao ban sao.
- Bo sung UX resize: khi tool `Mouse` dang chon image/sticker/text, dua chuot vao goc vung chon se hien cursor `SizeNWSE` (mui ten resize) de keo phong to/thu nho.
- Bo sung xoa object dang chon bang phim `Delete` (image/sticker/text). Xoa duoc dong bo qua mang va replay nho mo rong payload voi co `IsDeleted`.
- Nang cap UX resize object: ve ro 4 o vuong resize o ca 4 goc khung chon (khong con chi goc duoi-phai); cursor doi huong theo tung goc (`SizeNWSE`/`SizeNESW`) de thao tac truc quan hon.
- Sticky note ho tro click de chon (highlight) va nhan `Delete` de xoa note da chon; lenh xoa duoc phat qua `STICKY_NOTE` voi `IsOpen=false` de cac client khac remove note.
- User da xac nhan hoan thanh (accepted) nhom tinh nang thao tac doi tuong tren canvas: text/image/sticker co keo-tha + resize + `Delete`, va sticky note co click-select + `Delete` + dong bo xoa.
- Da bo khoi scope theo yeu cau user: xoay sticker bang UI va phan hoi/reply ghi chu. Sticker van giu keo-tha/resize/delete; sticky note van giu tao/sua/keo/resize/select/delete.
- Da bo khoi scope theo yeu cau user: hoan thien/goi y prompt AI. Client khong con nut `AI: Goi y prompt`, khong con `GeminiClient.GenerateTextAsync`, khong con command/payload `AI_AUTOCOMPLETE`.
- AI hien chi con 2 tinh nang active: Hugging Face Stable Diffusion text-to-image va Remove.bg xoa nen. Cap nhat 2026-05-25: user chuyen tu Google/Imagen sang Hugging Face access token. `.env`/`.env.example` dung `HF_TOKEN` va `HF_IMAGE_MODEL=stabilityai/stable-diffusion-xl-base-1.0`; khong con can `GEMINI_API_KEY`/`GEMINI_IMAGE_MODEL` cho tinh nang tao anh active. `DrawingClient/AI/StabilityAiClient.cs` goi Hugging Face Routing endpoint `https://router.huggingface.co/nscale/v1/images/generations` voi header `Authorization: Bearer <HF_TOKEN>`, body JSON `response_format="b64_json"`, `prompt`, `model`, roi doc anh tu `data[0].b64_json`. `MainForm` goi `StabilityAiClient.GenerateImageAsync`; server luu `AiResults.provider="huggingface"` va model theo `HF_IMAGE_MODEL`. Kiem thu API that ngay 2026-05-25 voi token hien tai pass, sinh PNG 1024x1024 tu prompt test. Remove.bg co 2 luong: neu dang chon image object tren canvas thi xoa nen va cap nhat lai dung object do bang cung `ActionID`; neu khong chon image thi chon file, xoa nen, tu chen giua canvas. User da xac nhan Remove.bg hoat dong on ngay 2026-05-25. Ca hai deu sync qua server, replay nhu `ImportImage`, luu `AiResults` va `DrawHistory`; server broadcast truoc, luu DB nen sau.
- Undo/redo da chuyen sang theo action cua chinh user: client luu lich su `DrawAction` theo `ActionID`/`Username`, moi stroke keo but dung chung mot `ActionID`, Ctrl+Z/Undo chi danh dau an dung 1 action gan nhat cua user hien tai va render lai lich su; Redo khoi phuc action tu stack cua user. Server dong dau `Username` tu session vao payload `UNDO`/`REDO`, luu vao `ActionStack`, va khi client join sau thi gui `SYNC_BOARD` truoc roi phat lai stack undo/redo de client moi thay dung trang thai hien tai.
- "Nan hinh thong minh" theo scope toi gian da hoan thien: khi ve Rectangle hoac Circle ma giu `Shift`, preview va net ve cuoi duoc ep thanh square/perfect circle.
- Cursor realtime va laser da toi uu lai ngay 2026-05-25 cho direct/LAN tren mot may: client khong con gui cursor theo mau thua 35ms trong UI thread, ma luu latest-state moi lan `MouseMove` va timer nen flush `CURSOR`/`LASER` moi ~8ms. Khi `_udpManager` san sang va `PreferTcpRealtime=false`, flush di UDP/AES; TCP chi la fallback khi relay/ngrok (`PreferTcpRealtime=true`) hoac UDP khong khoi tao duoc. Server UDP khong spam log cho `CURSOR`/`LASER`, bo echo ve sender de giam tai, va chi TCP fallback cho client trong room chua co UDP endpoint. Client nhan TCP/UDP deu raise cung `NetworkEvents`, bo qua packet cua chinh user, va render remote cursor/laser trong `CanvasManager` bang toa do canvas sau zoom/pan (`CanvasManager.ScreenToCanvas`).
- Da cap nhat `local/features.md` ngay 2026-05-25: bang tinh nang bay gio tach `✅` chac chan co luong user-facing/code ro, `⚠️` co mot phan/can demo that, `❌` chua dap ung mo ta, `BỎ` la ngoai pham vi. User da chot: observer chi xem/chat va bo reaction; undo/redo theo action cua chinh minh da lam; GIF export da bo hoan toan khoi scope; pixel art tich hop canvas 64x64; bo watermark; bo sticker rotate UI; bo sticky note replies; canvas size co dinh moi room, chi zoom local; snapshot UI don gian da bo khoi scope theo yeu cau moi.
- GIF export da bi loai bo hoan toan ngay 2026-05-25 theo yeu cau user sau khi tinh nang hoat dong khong on dinh. Khong con UI `Xuat GIF`, progress/status GIF, `ClientNetwork.SendExportGifRequest`, `NetworkEvents.OnGifExportProgress`, shared command `EXPORT_GIF_REQUEST`/`GIF_EXPORT_PROGRESS`, `SharedLib/Payloads/GifExportPayload.cs`, `DrawingServer/Services/GifExportService.cs`, `DrawingServer/Services/DrawHistoryCache.cs`, server handler export GIF, test `NT106Tests/GifExportServiceTests.cs`, hay schema `GifExports` trong migration/setup. Neu sau nay co yeu cau anh dong, phai xem nhu tinh nang moi va hoi user chot lai scope/contract.
- LB relay dang dung `LOAD_BALANCER_STRATEGY=room-affinity`: client join room hoi `ROUTE room=<roomCode>`, LB doc `Rooms.owner_server_id`, client reconnect qua `RELAY server=<server_id>` truoc TLS handshake de join dung backend owner cua room.
- Cac chang 01-07 da code/test boi Codex va duoc chuyen vao `local/done/`; con can user demo that de danh dau accepted neu chua co xac nhan.

## File quan trong can doc/sua

### Client

- `DrawingClient/Forms/LoginForm.cs`: login/register, quyet dinh ket noi direct/relay qua LB, subscribe auth events.
- `DrawingClient/Forms/LobbyForm.cs`: tao/join room; truoc join room relay se route/reconnect den owner server.
- `DrawingClient/Forms/MainForm.cs`: UI chinh, toolbars, AI buttons, chat/members, event handlers realtime, zoom local, toggle an/hien khung phai. Cursor/laser direct/LAN dung latest-state pump flush ~8ms qua UDP de tranh nhay theo mau thua; TCP chi fallback khi relay/ngrok hoac UDP loi.
- `DrawingClient/Forms/MainForm.cs`: quan ly lich su action client-side cho undo/redo theo `ActionID`/`Username`; chi undo/redo action gan nhat cua chinh user hien tai va render lai lich su visible.
- `DrawingClient/Drawing/CanvasManager.cs`: ve tren canvas 1920x1080 co dinh, fit viewport, pan/zoom local, mouse events, render draw actions, background mau/anh, image/sticker/text.
- `DrawingClient/Drawing/CanvasManager.cs`: rectangle/circle giu `Shift` khi keo chuot se ep thanh square/perfect circle; pen stroke trong mot lan keo dung chung `ActionID` de undo lui dung 1 stroke.
- `DrawingClient/Drawing/CanvasManager.cs`: quan ly object selection/manipulation cho image/sticker/text, hover cursor resize o goc, va API `DeleteSelectedObject()` de xoa object dang chon.
- `DrawingClient/Drawing/CanvasManager.cs`: resize object da chon bang 4 handle o 4 goc, co tinh toan resize theo tung goc (top-left/top-right/bottom-left/bottom-right).
- `DrawingClient/Drawing/CanvasManager.cs`: render remote cursor/laser theo toa do canvas sau transform zoom/pan; co `UpdateRemoteCursor`, `RemoveRemoteCursor`, `UpdateRemoteLaser`, `RemoveRemoteLaser`.
- `DrawingClient/Drawing/TextTool.cs`: TextBox editor tren viewport; commit text ve toa do canvas de sync/replay/manipulation khong lech khi zoom-pan.
- `DrawingClient/Network/ClientNetwork.cs`: TCP/TLS client, heartbeat, reconnect room-owner qua LB, send/receive packet; co `SendCursorRealtime`/`SendLaserRealtime` va nhan `CURSOR`/`LASER` TCP lam fallback cho relay/ngrok.
- `DrawingClient/Network/LoadBalancerRouteClient.cs`: goi `ROUTE` va `ROUTE room=<roomCode>` den LB.
- `DrawingClient/Network/UdpManager.cs`: UDP realtime local/direct, `UDP_PING` burst dang ky endpoint; cursor/laser uu tien UDP khi direct/LAN, con relay/ngrok TCP khong khoi tao UDP vi `PreferTcpRealtime=true` va dung TCP fallback trong `ClientNetwork`.
- `DrawingClient/UI/CursorLayer.cs`: hien chi con dung cho emoji/reaction overlay cu; khong con la noi render cursor/laser.
- `DrawingClient/AI/StabilityAiClient.cs`, `RemoveBgClient.cs`: Hugging Face Stable Diffusion text-to-image va remove background. `GeminiClient.cs` da bi go khoi project sau khi chuyen provider tao anh sang Hugging Face.

### Server

- `DrawingServer/Program.cs`: doc env, khoi dong TCP/UDP, heartbeat server node, cross-server sync.
- `DrawingServer/Network/SecureTcpServer.cs`: TCP/TLS protocol, login/room/chat/draw, relay fallback, fast-path broadcast, DB save nen; nhan `CURSOR`/`LASER` TCP de broadcast realtime tam thoi trong room ma khong luu DB.
- `DrawingServer/Network/SecureTcpServer.cs`: voi `IMPORT_IMAGE`, server giu `ActionID` tu payload khi save `DrawHistory` de replay dung luong update object.
- `DrawingServer/Network/SecureTcpServer.cs`: voi `UNDO`/`REDO`, server luu action vao `ActionStack` va khi client join se gui history roi replay undo/redo stack cho client moi.
- `SharedLib/Payloads/DrawPayload.cs`, `InteractionPayload.cs`, `SyncPayload.cs`: bo sung truong `IsDeleted` cho luong text/import-image/sticker de phat lenh xoa object ma khong can them command moi.
- `DrawingClient/Forms/MainForm.cs`: sticky note co luong select/delete rieng; khi nhan packet `STICKY_NOTE` ma `IsOpen=false` thi remove control note neu dang ton tai.
- `DrawingServer/Network/SecureUdpServer.cs`: UDP/AES realtime local/direct, endpoint registration, TCP fallback cho client chua co UDP endpoint, bao gom cursor/laser. Tranh log moi goi cursor/laser va khong echo pointer ve sender de giu latency thap.
- `DrawingServer/Services/RoomService.cs`: active room state, member list, room owner, turn-based state.
- `DrawingServer/Services/CrossServerSyncService.cs`: PostgreSQL `LISTEN/NOTIFY` fallback khi room bi chia server.
- `DrawingServer/Services/ServerNodeHeartbeatService.cs`: upsert `ServerNodes` de theo doi backend.
- `DrawingServer/Services/Database/DbManager.cs`: Neon/PostgreSQL access, rooms, draw history, chat, gallery, AI, snapshots, pixel art.
- `DrawingServer/Services/Database/DbManager.cs`: co `SaveActionStackAsync`, `GetActionStackEntriesAsync`, `ClearActionStackAsync` cho undo/redo persistent theo room.
- `DrawingServer/Services/Database/Migrations/*.sql`: migration chinh.

### Load balancer

- `LoadBalancer/LoadBalancer.cs`: health check TLS, `ROUTE`, `RELAY`, room-affinity, proxy stream.
- `LoadBalancer/Program.cs`: doc env, load `servers.json` hoac fallback env `LB_SERVER_*`.
- `LoadBalancer/servers.json`: backend list cho demo local/LAN/Tailscale.
- `LoadBalancer/LoadBalancer.csproj`: SDK-style, `Npgsql 8.0.9`, `Newtonsoft.Json 13.0.4`, output tai `LoadBalancer/bin/Debug/LoadBalancer.exe`.

### Shared contracts

- `SharedLib/Packets/PacketDef.cs`: enum command. Them command moi phai sua o day truoc.
- `SharedLib/Packets/PacketHelper.cs`: serialize/deserialize packet payload.
- `SharedLib/Payloads/*.cs`: DTO client-server.
- `SharedLib/Payloads/DrawPayload.cs`: `SetBackgroundPayload` co `ImageData` base64 tuy chon de dong bo anh nen; rong/null nghia la nen mau.
- `SharedLib/Config/EnvLoader.cs`: load `.env`, env process co uu tien hon file `.env`.
- `SharedLib/Security/AesHelper.cs`, `SecurityConfig.cs`: AES UDP.

### Tai lieu local

- `local/features.md`: bang tinh nang user-facing va trang thai.
- `local/plan/status_check.md`: bang dieu khien tien do hien tai.
- `local/plan/requirements.md`: yeu cau va quyet dinh da chot.
- `local/plan/plan.md`: workflow lam viec theo chang.
- `local/plan/08_kiem_thu_va_tieu_chi_hoan_thanh.md`: checklist test.
- `local/plan/09_rui_ro_va_uu_tien.md`: rui ro/backlog dang theo doi.
- `local/plan/10_runbook_demo_va_doi_soat.md`: cach user chay thu va xac nhan.
- `local/plan/11_nhat_ky_thuc_thi_2026-05-24.md`: nhat ky.
- `local/done/`: chang da code/test xong boi Codex.
- `local/setup/setup.md`, `scenario-*.ps1`: runbook/script demo local, LAN, internet/ngrok.
- `role-setup/`: goi script theo role cho client/load balancer/server; ho tro local, LAN va playit internet direct/relay.
- `local/plan/12_kich_ban_demo_playit.md`: kich ban demo playit, phan biet TCP/UDP direct va TCP relay qua LB.
- `local/plan/13_cau_hoi_dua_du_an_vao_thuc_te.md`: danh sach cau hoi can chot truoc khi dua du an vao thuc te.

## Realtime drawing: duong di va diem nghen

Co 2 mode:

- Direct/LAN: client ket noi thang server hoac LB route truc tiep backend. Draw/flood fill/text di TCP reliable; UDP uu tien cho cursor/laser va endpoint registration, co TCP fallback cho client chua co UDP endpoint. Cursor/laser duoc gui theo latest-state moi ~8ms, bo qua cac vi tri cu neu da co vi tri moi hon, de ben nhan thay trang thai hien tai thay vi backlog diem qua khu.
- Relay/ngrok: client chi ket noi TCP vao LB/ngrok, drawing/flood fill/text va cursor/laser gui qua TCP vi ngrok TCP khong relay UDP.

Duong relay hien tai:

1. Client login vao LB relay.
2. Khi join room cu, `LobbyForm` goi `ClientNetwork.ReconnectToRoomOwnerViaLoadBalancerAsync`.
3. Client goi `LoadBalancerRouteClient.ResolveAsync(lbHost, lbPort, roomCode)`.
4. LB doc `Rooms.owner_server_id` trong Neon va tra `serverId`.
5. Client reconnect den LB, gui preface `RELAY server=<serverId>` truoc TLS handshake.
6. LB proxy phien TLS den dung backend.
7. Client login lai nen, gui `JOIN_ROOM`.
8. Khi ve, server `SecureTcpServer` broadcast packet cho client cung room truoc, roi moi luu `DrawHistory` nen.

Diem can tranh:

- Khong de client cung room roi vao 2 backend khac nhau; neu bi chia, net ve phai di qua `CrossServerSyncService`/PostgreSQL notify va se cham.
- Khong `await DbManager.SaveStrokeAsync` truoc broadcast cho net ve realtime.
- Khong dua draw/flood fill/text quan trong qua UDP trong direct/LAN. UDP co the roi goi nen se lam mat vinh vien tung doan stroke tren may khac; neu can dam bao khong mat net, client phai gui cac lenh nay bang TCP (`ClientNetwork.SendDrawRealtime`, `SendFloodFillRealtime`, `SendTextRealtime`).
- Doi voi image/sticker/text co thao tac move/resize, giu nguyen `ActionID` khi gui update. Client replay can map `ActionID` vao object hien co; neu doi `ActionID` moi lan se tao ban sao khong mong muon.
- Khi xoa image/sticker/text, gui cung `ActionID` va dat `IsDeleted=true`. Client nhan se remove object khoi object layer; replay tu `DrawHistory` se cho ket qua cuoi cung dung.
- Khi xoa sticky note, gui `STICKY_NOTE` voi `NoteID` + `IsOpen=false`; ben nhan phai xoa note control khoi canvas va clear trang thai selected neu trung note dang chon.
- Anh nen canvas dung chung command `SET_BACKGROUND`; khi replay `SYNC_BOARD`, client can doc ca `ColorARGB` va `ImageData` cua action `SetBackground`. Eraser xoa lop ve ve trong suot de lo nen mau/anh ben duoi.
- Neu log LB co `Room owner lookup failed`, kiem tra `DATABASE_URL`, Npgsql version, schema `Rooms.owner_server_id`.
- Neu log LB route vao server sai, kiem tra `LoadBalancer/servers.json` `server_id` phai khop env `SERVER_ID` cua drawing server.

## Cau hinh moi truong quan trong

`.env` khong commit. `.env.example` co placeholder.

Client/LB:

- `USE_LOAD_BALANCER_ROUTING=1`
- `LOAD_BALANCER_CLIENT_MODE=relay`
- `LOAD_BALANCER_STRATEGY=room-affinity`
- `LOAD_BALANCER_HOST=127.0.0.1` hoac host ngrok
- `LOAD_BALANCER_PORT=9000` hoac port ngrok

Server:

- `SERVER_ID=server-1` / `server-2` phai khop `LoadBalancer/servers.json`.
- `SERVER_NAME`, `SERVER_TCP_PORT`, `SERVER_UDP_PORT`, `SERVER_PUBLIC_HOST`.
- `SERVER_CERT_PATH=server.pfx`, `SERVER_CERT_PASSWORD=...`.
- `DATABASE_URL=Host=your-db-host;Port=5432;Database=drawingapp;Username=your_user;Password=your_password;

AI:

- `HF_TOKEN`, `HF_IMAGE_MODEL` (mac dinh `stabilityai/stable-diffusion-xl-base-1.0`; Hugging Face Routing dang goi nscale image generation endpoint).
- `REMOVE_BG_API_KEY`.

## Build, test, demo

Restore:

```powershell
dotnet restore .\NT106_DrawingApp.sln /p:RestorePackagesConfig=true
```

Build:

```powershell
dotnet build .\NT106_DrawingApp.sln -v:minimal
```

Test:

```powershell
dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal
```

Demo local nhanh:

```powershell
powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-1-local.ps1
```

## Database schema can nho

- `Users`: tai khoan.
- `Rooms`: room metadata, `room_code`, `owner_id`, `owner_server_id`, canvas size, max members.
- `DrawHistory`: stroke replay cho client vao sau.
- `ChatHistory`, `Gallery`, `AiResults`, `Snapshots`, `PixelArtCells` cho chat, gallery, AI, snapshot backend va pixel art.
- `ServerNodes`: heartbeat backend.
- `RoomEvents`: cross-server events va notify payload.
- Cac bang da loai bo khoi schema cloud ngay 2026-05-25 vi rong/khong con active code dung: `RoomMembers`, `StickyNotes`, `StickyNoteReplies`, `Stickers`, `ClientRateLimits`, `GifExports`.

## Tinh nang chinh

- Canvas GDI+, free draw, shape, eraser, color/thickness/background mau hoac anh, zoom local; viewport canvas co dinh 1920x1080, zoom-out toi da se cover khung nhin thay de moi diem dang thay deu ve duoc. Rectangle/Circle giu `Shift` de ve square/perfect circle.
- Flood fill BFS, text tool, import image, sticker, sticky note, claim area.
- Chat, member list, user join/leave, sync board replay, undo/redo theo action gan nhat cua chinh user.
- Cursor/laser realtime: UDP local/direct khi co endpoint, TCP fallback qua relay/ngrok; reaction da bo khoi scope.
- Turn-based drawing co owner control va auto-advance khi user roi phong.
- AI Hugging Face Stable Diffusion text-to-image va Remove.bg; server sync/replay ket qua AI. Tinh nang hoan thien/goi y prompt da bi loai khoi scope.
- Gallery/save/export anh tinh khong watermark theo quyet dinh moi; GIF export da bo khoi scope va khong con UI/protocol/service.
- Pixel art la tinh nang to mau cong tac theo tung pixel, user chot tich hop vao canvas voi grid 64x64; hien co payload/server storage, UI/grid/rate-limit chua hoan chinh.

## Backlog va rui ro

- User can demo that 3 client/2 server/LB/ngrok/Neon de xac nhan latency muc tieu 0.3-0.5s.
- Theo `local/features.md`, cac tinh nang dang `❌` can trien khai theo quyet dinh moi: spectator chi xem/chat, khoa vung ve enforce server + client render/chan som, pixel art UI 64x64 tich hop canvas. Undo/redo cua chinh user va Shift-square/circle da lam; GIF export, sticker rotate UI, sticky note replies va snapshot UI don gian da bo khoi scope.
- Cac tinh nang da bo khoi scope: reaction, watermark export/gallery, thay doi/sync canvas size.
- Cac tinh nang dang `⚠️`/can demo hoac polish: follow viewport pan+zoom, turn-based UX 2 client, snapshot backend/timeline, AI voi key that, ngrok/Tailscale demo.
- Cross-server sync chi nen la fallback; khong phu hop cho net ve lien tuc neu can latency thap.

## Quyet dinh user da chot 2026-05-25

- Khoa vung ve: Codex chon phuong an hop ly; mac dinh nen enforce tren server, client render/chan som.
- Observer: chi xem va chat; bo reaction.
- Undo/redo: chi action cua chinh user; da implement theo `ActionID`/`Username`, moi lan keo pen la 1 action.
- GIF export: da bo hoan toan khoi scope hien tai; khong con UI, protocol, service, payload, test hay schema lien quan.
- Pixel art: tich hop vao canvas, grid 64x64.
- Watermark: bo hoan toan khi export/gallery.
- Sticker rotate: da bo khoi scope; khong can UI xoay.
- Canvas size: co dinh trong moi room; moi user zoom local theo y muon.
- Snapshot UI don gian: da bo khoi scope hien tai theo yeu cau moi; khong can bo sung UI xem/chon snapshot.

## Quy tac lam viec tiep

- Neu sua protocol, sua `SharedLib` truoc va build ca solution.
- Neu sua realtime, uu tien critical path: broadcast truoc, IO/DB/log nang lam nen sau.
- Canvas size khong con la tuy chon UI; giu 1920x1080, zoom-out toi da cover viewport, pan duoc clamp de khong lo nen xam/ngoai canvas, va chi doi zoom/pan local neu can.
- Neu sua LB/room routing, giu `server_id` dong bo giua `.env`, `servers.json`, `Rooms.owner_server_id`.
- Neu gap yeu cau moi co kha nang doi schema, hoi user truoc khi migration pha du lieu.
- Sau moi thay doi dang ke: build, test, cap nhat `local/description.md`, `local/plan/status_check.md`, va file lien quan.
