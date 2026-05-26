### Trang thai tinh nang - cap nhat 2026-05-25

Trang thai: `ACTIVE`

## Cap nhat nhanh 2026-05-26

- Da sua loi kich ban 1 mat ket noi do `setup/apps/DrawingServer` thieu DLL phu thuoc sau lan package bi lock/copy do dang. Da copy bo sung dependency tu Release output vao `setup/apps/DrawingServer`, smoke `start-local-no-lb.ps1 -ClientCount 0 -StopExisting` pass TLS, va `package-release.ps1` bay gio fail som neu co app dang chay tu `setup/apps`.
- Da chot giu cross-server sync/listen-notify lam fallback, khong tat. `LoadBalancer` duoc gia co cho route room trong kich ban 5.3: owner da biet nhung health stale se duoc ping lai ngay; room legacy co `owner_server_id` rong se duoc claim owner on dinh theo hash room va update DB; room khong thay trong DB cua LB se log ro de bat loi LB/server khac `DATABASE_URL`.
- Da sua loi chat khong tu xuong dong khi tin nhan dai: `MainForm` doi vung hien thi Chat tu `ListBox` sang `RichTextBox` read-only co `WordWrap=true`, tu dong scroll den dong cuoi khi co tin nhan moi; logic chat/network giu nguyen.
- Da fix loi server/DB `53300: remaining connection slots...` gay mat net/replay thieu net: draw/flood/text/spray/sticker/background/import/AI image duoc dua vao `StrokePersistenceQueue` de luu `DrawHistory` tuan tu co retry/backoff, khong mo connection DB rieng cho tung packet tren critical path. `SYNC_BOARD` nay lay ca history DB va pending stroke trong RAM, nen user vao phong sau van thay du net da ve trong runtime ngay ca khi DB dang cham/het slot tam thoi.
- Da fix client moi vao phong co history lon nhung canvas trang/bi disconnect: server chia `SYNC_BOARD` thanh chunk nho bang `SyncBoardPayload.RawActions`, client gom chunk va render khi nhan chunk cuoi; packet limit client/server tang len 50MB cho action anh don le. Session TCP bi write fail duoc danh dau disconnected de broadcast bo qua, giam log `forcibly closed`.
- Da gia co LoadBalancer room-affinity: `ROUTE room=<roomCode>` phai tra dung `owner_server_id`/cache owner; neu khong lookup duoc owner hoac owner unhealthy thi fail closed thay vi route random sang server khac. Client join room qua LB se dung lai neu khong resolve duoc owner, tranh chia cung phong sang 2 backend.
- `PostgresConnectionString.Normalize` chi tu them `Timeout=15` neu env chua set; khong chen `Max Pool Size` vi Npgsql runtime hien bao `Couldn't set max pool size`. `CrossServerSyncService` serialize publish bang semaphore, va cross-server notify bo qua draw/flood/text/spray tan suat cao de tranh connection storm.
- Da build lai solution, test pass 20 skip 1, va chay `setup/package-release.ps1` de cap nhat `setup/apps` + `NT106-DrawingApp-setup.zip`.
- Da fix/smoke kich ban `setup/start-local-no-lb.ps1`: server readiness doi bang TLS handshake that, client duoc ep direct mode (`USE_LOAD_BALANCER_ROUTING=0`, `LOAD_BALANCER_CLIENT_MODE=direct`, `SERVER_PUBLIC_HOST=127.0.0.1`) de tranh mo nham LB relay khi demo 1 server. Smoke helper connect SSL + login auto-register pass.
- Da fix runtime database DigitalOcean: `SharedLib.Config.PostgresConnectionString.Normalize` chuan hoa `postgresql://...?...sslmode=require` sang key-value Npgsql; `DbManager`, `CrossServerSyncService` va `LoadBalancer` khong con loi `Couldn't set postgresql://...sslmode`.
- User chot khong copy du lieu ung dung tu Neon sang DigitalOcean, chi can schema giong. Da schema-only reset DigitalOcean va apply migration 001-005; doi soat Neon/DO khop 10 bang active va DO de trong row ung dung.
- `.env` root va `setup\.env` deu tro DigitalOcean; `setup\.env.example` ghi ro chap nhan ca URI DigitalOcean va key-value Npgsql.
- Helper `local/tmp_neon_migrator` co them `MIGRATOR_ENV_PATH`, `schema-only`/`reset-schema`, va `shape`/`schema-audit` de reset/doi soat schema khong copy du lieu.
- Da bo snapshot tu dong/UI/backend va khoa vung ve khoi scope/code/schema: khong con `SnapshotService`, command/payload snapshot, command/payload claim area, UI Shift+right-drag chon vung, DB API snapshot hay bang `Snapshots`; Neon online da drop `Snapshots`.
- Da sua ve theo luot: chu phong bat/tat mode, con nut/chuc nang chuyen luot chi thuoc user dang giu `ActiveDrawingUser`; server TCP/UDP cung chan `TURN_CHANGE` tu nguoi khac.
- Da fix loi UI mat nut o dong duoi toolbar trong `DrawingClient/Forms/MainForm.cs` bang cach doi layout ve dang compact 3 dong, cao `112px`, moi dong co cum trai/phai de tan dung khoang trong ben phai va tranh tran man hinh.
- Da nhom lai toolbar theo huong thao tac nhanh giong Paint: `Dieu huong`, `Nen`, `Canvas`, `Tep/Luu`, `AI`, `Sticker` (tool ve hien icon nho + tooltip).
- Da bo UI follow user khoi `MainForm` (textbox follow, nut follow, label follow) va bo lang nghe event follow o man hinh chinh.
- Da build thanh cong toan bo solution va package release vao `setup/apps` qua `setup/package-release.ps1`.
- Toan bo project backend lien quan DB hien dong bo voi `Npgsql 8.0.9`; helper `local/tmp_neon_migrator` co the audit row count va copy active schema/data sang DB PostgreSQL dich bang `TARGET_DATABASE_URL`.
- `setup/package-release.ps1` da duoc dung de rebuild release binary va tao lai `NT106-DrawingApp-setup.zip`; day la luong build chuan cho `setup/apps`.

Quy uoc:

- `âœ…` da co luong user-facing ro rang trong client/server va co the xem la da thuc hien duoc theo code hien tai.
- `âš ï¸` da co mot phan code hoac can dieu kien ngoai he thong/user demo that de xac nhan.
- `âŒ` chua co luong user-facing day du, hoac code hien tai khong dap ung mo ta tinh nang.
- `Bá»Ž` da duoc user loai khoi pham vi, khong can trien khai.

## Xac nhan nghiem thu boi user (2026-05-25)

- Da xac nhan hoan thanh: text tool luu dung toa do canvas va cho phep keo-tha/chinh co/`Delete`.
- Da xac nhan hoan thanh: image import co the keo-tha, resize 4 goc va `Delete` sau khi dat len canvas.
- Da xac nhan hoan thanh: sticker co the keo-tha, resize 4 goc va `Delete` sau khi dat len canvas.
- Da xac nhan hoan thanh: sticky note co select bang click, resize, va `Delete` (dong bo xoa qua `IsOpen=false`).

## Nhom A: cong cu ve co ban

| trang thai | ten tinh nang | ket luan hien tai | file lien quan |
| --- | --- | --- | --- |
| âœ… | canvas GDI+ | Co `PictureBox` double-buffered, `Bitmap` lam lop ve va render bang GDI+. Canvas logic duoc chot co dinh 1920x1080 trong moi room; zoom-out toi da cover viewport de khong con khoang chet trong vung dang thay. Da duoc user test va xac nhan. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs` |
| âœ… | ve tu do | Co tool Pen ve theo cac doan line lien tiep khi keo chuot; sync qua TCP reliable. | `DrawingClient/Drawing/CanvasManager.cs`, `DrawingClient/Forms/MainForm.cs` |
| âœ… | ve hinh hoc co ban | Co Line, Rectangle, Circle. Chua co triangle/shape nang cao. | `DrawingClient/Drawing/DrawingTools.cs`, `DrawingClient/Drawing/CanvasManager.cs` |
| âœ… | cuc tay | Co tool Eraser; hien tai xoa lop net ve de lo nen mau/anh ben duoi. | `DrawingClient/Drawing/CanvasManager.cs` |
| âœ… | bang mau | Co `ColorDialog`, cap nhat mau net hien tai. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs` |
| âœ… | do day net ve | TrackBar 1-30px dieu khien do day net. | `DrawingClient/Forms/MainForm.cs` |
| âœ… | xoa toan bo | Clear canvas local, server xoa `DrawHistory`, broadcast `CLEAR_ALL`. | `DrawingClient/Drawing/CanvasManager.cs`, `DrawingServer/Network/SecureTcpServer.cs` |

## Nhom B: cong cu ve nang cao

| trang thai | ten tinh nang | ket luan hien tai | file lien quan |
| --- | --- | --- | --- |
| âœ… | to mau BFS | Co flood fill bang BFS va sync/replay. | `DrawingClient/Drawing/FloodFill.cs`, `DrawingClient/Drawing/CanvasManager.cs` |
| âœ… | cong cu van ban | Co text tool tao TextBox, luu text dung he toa do canvas (khong mat chu khi pan/zoom), sync/replay va cho phep keo-tha/chinh co chu bang tool `Mouse`; hien 4 o vuong resize ro o 4 goc vung chon (cursor doi huong theo goc), nhan `Delete` de xoa text dang chon. | `DrawingClient/Drawing/TextTool.cs`, `DrawingClient/Drawing/CanvasManager.cs`, `DrawingClient/Forms/MainForm.cs` |
| âœ… | zoom in/out ca nhan | Co zoom factor, nut Zoom +/-, `Ctrl + MouseWheel`, `Ctrl +` va `Ctrl -`; user duoc zoom rieng, khong sync cho ca phong; zoom-out toi da cover viewport. Da duoc user test va xac nhan. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs` |
| âœ… | chuot / pan viewport | Co tool `Chuá»™t` de khong ve net; giu chuot trai keo de pan viewport, phuc vu zoom va di chuyen trong canvas; pan duoc clamp de khong lo nen ngoai canvas. Da duoc user test va xac nhan. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs`, `DrawingClient/Drawing/DrawingTools.cs` |
| âœ… | mau nen canvas | Doi mau nen, sync bang `SET_BACKGROUND`, replay trong history. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs` |
| âœ… | chon anh lam nen | Da co nut `Anh nen`, chon file anh local, resize/nen ve kich thuoc canvas, sync/replay bang `SET_BACKGROUND.ImageData`. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs`, `SharedLib/Payloads/DrawPayload.cs` |
| âœ… | hut mau | Pipette lay mau pixel tren lop ve hien tai. | `DrawingClient/Drawing/CanvasManager.cs` |
| âœ… | nhap anh | Chon file anh, keo chon vung dat anh, chen vao canvas va sync/replay; sau khi dat van co the keo-tha/doi kich thuoc bang tool `Mouse` va dong bo theo `ActionID`. Hien 4 o vuong resize o 4 goc vung chon (cursor doi huong theo goc), nhan `Delete` de xoa anh dang chon. Chua co crop nang cao. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs`, `DrawingClient/Network/ClientNetwork.cs`, `SharedLib/Payloads/DrawPayload.cs` |
| Bá»Ž | thay doi kich thuoc canvas dong bo | User chot canvas co dinh hoan toan trong moi room, chot 1920x1080; khong con combobox chon size tren sidebar. Moi nguoi chi zoom/pan local theo y muon. | Da vo hieu hoa UI resize |
| Bá»Ž | watermark export/gallery | User chot bo watermark va bo noi dung watermark. Export/Gallery khong can dong dau ban quyen. | Khong can trien khai |
| âœ… | thu vien hinh dan co ban | Co picker sticker, keo dat kich thuoc, sync/replay; sticker da dat co the keo-tha/doi kich thuoc bang tool `Mouse` (update theo `ActionID`, khong tao ban sao khi move). Hien 4 o vuong resize o 4 goc vung chon (cursor doi huong theo goc), nhan `Delete` de xoa sticker dang chon. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs`, `DrawingClient/Network/ClientNetwork.cs`, `SharedLib/Payloads/InteractionPayload.cs`, `SharedLib/Payloads/SyncPayload.cs` |
| Bá»Ž | xoay sticker bang UI | User chot bo tinh nang xoay sticker bang UI; sticker hien giu keo-tha/resize/delete la du trong scope hien tai. | Khong can trien khai |
| âœ… | nan hinh thong minh | Theo scope moi cua user: khi ve Rectangle/Circle va giu `Shift`, client ep Rectangle thanh hinh vuong va Circle thanh hinh tron trong preview lan net ve cuoi. | `DrawingClient/Drawing/CanvasManager.cs` |

## Nhom C: cong tac qua mang

| trang thai | ten tinh nang | ket luan hien tai | file lien quan |
| --- | --- | --- | --- |
| âœ… | dong bo net ve realtime | Draw/flood fill/text di TCP reliable, server broadcast truoc roi dua vao `StrokePersistenceQueue` luu DB nen co retry/backoff. Queue giu pending stroke theo room trong RAM, nen client join sau nhan ca history DB va stroke chua flush. History lon duoc gui bang chunked `SYNC_BOARD` de tranh packet qua lon lam client disconnect/canvas trang. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Network/ClientNetwork.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `DrawingServer/Services/Database/StrokePersistenceQueue.cs`, `SharedLib/Payloads/SyncPayload.cs` |
| âœ… | con tro thoi gian thuc | Cap nhat 2026-05-25 theo yeu cau moi: chi giu cursor, bo laser. Client lay toa do canvas sau zoom/pan moi lan `MouseMove`, gan `RoomCode` + `Timestamp`, luu latest-state va flush `CURSOR` bang UDP/AES moi ~12ms khi `_udpManager` san sang (`PreferTcpRealtime=false`), hoac TCP fallback qua `ClientNetwork` khi relay/ngrok/UDP khong kha dung. Ben nhan gom latest cursor theo user va render bang timer UI ~15ms, bo packet cu hon theo `Timestamp` de tranh hang doi UI gay tre. Server UDP khong spam log cursor, khong echo ve sender, chuan hoa `Username`/`RoomCode`/`Timestamp`, va chi TCP fallback cho client chua co UDP endpoint; `UdpManager` cache endpoint server de tranh resolve host moi packet. Build/test pass. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs`, `DrawingClient/Network/UdpManager.cs`, `DrawingClient/Network/ClientNetwork.cs`, `SharedLib/Payloads/InteractionPayload.cs`, `DrawingServer/Network/SecureUdpServer.cs`, `DrawingServer/Network/SecureTcpServer.cs` |
| Bá»Ž | con tro laser | User yeu cau bo tinh nang laser; client khong con gui laser bang Alt, khong subscribe/nhan/render laser, `CanvasManager` khong con remote laser layer, TCP fallback khong con handler laser, UDP server bo qua `LASER` legacy. | Khong can trien khai |
| Bá»Ž | bieu tuong cam xuc/reaction | User chot bo reaction; uu tien che do quan sat chi xem va chat. | Can go/vo hieu hoa UI phim so reaction neu polish |
| âŒ | che do quan sat | Da chot observer chi xem va chat. Chua co UI join spectator, server member info/permission va client disable drawing theo spectator. | Can bo sung |
| Bá»Ž | khoa vung ve | Da bo khoi scope ngay 2026-05-26. Khong con UI chon vung, command/payload `CLAIM_AREA`/release/extend, hay server handler lien quan. | Khong can trien khai |
| âœ… | thong bao noi | `ToastForm` dang duoc goi cho nhieu thao tac UI/join/leave. | `DrawingClient/UI/ToastForm.cs`, `DrawingClient/Forms/MainForm.cs` |
| âœ… | hop tro chuyen | Chat realtime qua TCP, luu `ChatHistory`, gui lai 50 tin gan nhat khi join; UI Chat da sua word-wrap cho tin nhan dai (khong con tran ngang). | `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `DrawingServer/Services/Database/DbManager.cs` |
| âœ… | undo/redo theo action cua chinh minh | Client luu lich su action theo `ActionID`/`Username`; moi lan keo but dung chung mot `ActionID` nen undo chi lui dung 1 stroke/action gan nhat cua chinh user. Server luu `UNDO/REDO` vao `ActionStack` va phat lai stack nay sau `SYNC_BOARD`, nen client vao phong sau van thay dung trang thai da undo/redo. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `DrawingServer/Services/Database/DbManager.cs` |
| âœ… | danh sach thanh vien | Tab Members hien username/role/status/color, server broadcast `ROOM_MEMBERS`. Role spectator hien chua dung vi spectator chua hoan thien. | `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Services/RoomService.cs` |
| âš ï¸ | theo doi goc nhin | Co payload/nut Follow, hien moi ap dung zoom khi target nhan event; chua co pan/viewport source day du. | `DrawingClient/Forms/MainForm.cs`, `SharedLib/Payloads/InteractionPayload.cs` |
| âœ… | ghi chu dan co ban | Co sticky note tren canvas, drag/sua text va sync qua `STICKY_NOTE`; bo sung resize bang grip goc duoi-phai va dong bo `Width/Height`. Co the click de chon sticky note (highlight), nhan `Delete` de xoa sticky note da chon va dong bo xoa qua `IsOpen=false`. | `DrawingClient/Forms/MainForm.cs`, `SharedLib/Payloads/InteractionPayload.cs` |
| Bá»Ž | phan hoi ghi chu | User chot bo tinh nang reply/phan hoi ghi chu khoi scope hien tai; sticky note co ban van giu tao/sua/keo/resize/select/delete. | Khong can trien khai |
| âœ… | ve theo luot | Chu phong bat/tat turn-based; chi user dang giu `ActiveDrawingUser` thay/bam duoc nut chuyen luot. Server TCP/UDP enforce `TURN_CHANGE` chi hop le tu active user va van chan thao tac ve khi khong den luot. | `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `DrawingServer/Network/SecureUdpServer.cs`, `DrawingServer/Services/RoomService.cs` |
| âœ… | toolbar kieu Paint toi gian | Toolbar da duoc chuyen sang cac dải phang, khong con khung vuong chia group; toan bo cong cu van giu day du va sap xep gon thanh cac dải `Vẽ`, `Điều hướng`, `Nền`, `Canvas`, `Tệp/Lưu`, `AI`, `Sticker`. Da bo thanh header nho nam phia tren tab Chat; khung ben phai chi con Members/Chat/Nhat ky va o nhap chat duoi cung. Khi rebuild de chay bang `setup/start-local-no-lb.ps1`, can output vao `setup/apps` de script nap dung binary moi. | `DrawingClient/Forms/MainForm.cs` |

## Nhom D: nguoi dung, phong, luu tru

| trang thai | ten tinh nang | ket luan hien tai | file lien quan |
| --- | --- | --- | --- |
| âœ… | dang ky va dang nhap | Login/register qua TCP/TLS, password hash SHA256 trong DB. | `DrawingClient/Forms/LoginForm.cs`, `DrawingServer/Services/Database/DbManager.cs` |
| âœ… | tao va tham gia phong | Tao room 6 so, join room, gioi han thanh vien mac dinh. Canvas size duoc chot co dinh. | `DrawingClient/Forms/LobbyForm.cs`, `DrawingServer/Services/RoomService.cs`, `DrawingServer/Network/SecureTcpServer.cs` |
| âœ… | dong bo bang ve cu | Client join nhan `SYNC_BOARD` tu `DrawHistory` va replay actions. | `DrawingClient/Network/ClientNetwork.cs`, `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Network/SecureTcpServer.cs` |
| âœ… | thu vien ban ve/gallery | Luu canvas vao Gallery, lay danh sach, hien thumbnail va tai anh tu Gallery. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Forms/GalleryForm.cs`, `DrawingServer/Services/Database/DbManager.cs` |
| âœ… | xuat anh | Export canvas hien tai ra file anh. User chot khong watermark. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs` |
| Bá»Ž | xuat GIF | User yeu cau loai bo hoan toan tinh nang export GIF ngay 2026-05-25. Da go UI `Xuat GIF`, progress/status, client event/network method, shared payload/command, server handler/service/cache, test GIF va schema `GifExports`. Chi giu xuat anh tinh PNG/JPEG hien co. | Khong can trien khai |
| Bá»Ž | snapshot tu dong/UI/backend | Da bo khoi scope ngay 2026-05-26. Khong con `SnapshotService`, command/payload `SNAPSHOT_LIST`/`SNAPSHOT_RESTORE`, DB API snapshot hay bang `Snapshots`; Neon online da drop bang nay. | Khong can trien khai |
| âš ï¸ | timeline/time travel | Server con command timeline va lay history den timestamp truc tiep tu `DrawHistory`; tinh nang nay doc history, khong dung snapshot. | `DrawingServer/Network/SecureTcpServer.cs`, `SharedLib/Payloads/SyncPayload.cs` |
| âŒ | pixel art 64x64 tich hop canvas | User chot pixel art tich hop vao canvas, grid 64x64. Hien moi co payload/server DB save/sync, chua co UI grid/rate limit user-facing trong client. | Can bo sung |

## Nhom E: bao mat va ha tang

| trang thai | ten tinh nang | ket luan hien tai | file lien quan |
| --- | --- | --- | --- |
| âœ… | TCP/TLS protocol | TCP duoc boc `SslStream`, co packet framing va heartbeat. | `DrawingClient/Network/ClientNetwork.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `SharedLib/Packets/PacketDef.cs` |
| âœ… | UDP/AES cho tin hieu tam thoi | UDP ma hoa AES dung cho cursor/endpoint registration o direct/LAN. Cursor direct/LAN duoc gui theo latest-state pump ~12ms, server bo log/echo pointer tan suat cao de giam latency; ben nhan render latest-only ~15ms de khong backlog UI; TCP chi fallback khi relay/ngrok hoac client chua co UDP endpoint. Draw quan trong da chuyen TCP de tranh mat net. Laser va reaction da bi loai khoi scope. | `DrawingClient/Network/UdpManager.cs`, `DrawingClient/Network/ClientNetwork.cs`, `DrawingServer/Network/SecureUdpServer.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `SharedLib/Security/AesHelper.cs` |
| âœ… | server da luong | Server accept nhieu TCP client, moi client co task/stream lock rieng. | `DrawingServer/Network/SecureTcpServer.cs`, `DrawingServer/Network/ClientSession.cs` |
| âœ… | PostgreSQL/DigitalOcean schema | Runtime DB da chuyen sang DigitalOcean PostgreSQL managed. App chap nhan URI `postgresql://...?...sslmode=require` nho `PostgresConnectionString.Normalize`; helper nay chi tu them `Timeout=15` neu env chua set va khong chen `Max Pool Size` de tranh loi Npgsql runtime. DigitalOcean da schema-only reset/apply migrations 001-005, khop 10 bang active Users, Rooms, DrawHistory, ChatHistory, Gallery, AI, PixelArt, ServerNodes, RoomEvents, ActionStack; khong copy du lieu ung dung va khong con `Snapshots`. | `SharedLib/Config/PostgresConnectionString.cs`, `DrawingServer/Services/Database/DbManager.cs`, `DrawingServer/Services/Database/Migrations/*.sql`, `local/tmp_neon_migrator/Program.cs` |
| âœ… | load balancer room-affinity | LB co health check TLS, ROUTE, RELAY va room owner affinity. Khi route room cu, LB bat buoc dung `Rooms.owner_server_id` hoac cache owner; neu khong biet owner/owner unhealthy thi fail closed thay vi fallback random, va client dung join de tranh chia cung room qua nhieu backend. | `LoadBalancer/LoadBalancer.cs`, `DrawingClient/Network/LoadBalancerRouteClient.cs`, `DrawingClient/Network/ClientNetwork.cs` |
| âš ï¸ | demo internet/ngrok/Tailscale | Setup demo Internet dung ngrok. `setup/` van la goi duy nhat: zip giai nen ra `<demo-folder>\setup\...`, moi role tai cung mot zip, chay exe build san, script load `setup\.env`. Da sua loi `-Host` PowerShell bang alias sang `TargetHost`; cac script role khong hoi port nua, chi hoi host/IP khi thieu. `start-local-no-lb.ps1` da duoc gia co cho demo 1 server: doi server san sang bang TLS handshake va mo client direct vao `127.0.0.1:8888`, khong di qua LB relay. `start-load-balancer.ps1` hoi `Server-1 host/IP`/`Server-2 host/IP` voi default `127.0.0.1` la cung may, co huong dan lay IP bang `ipconfig`/`tailscale ip -4`. Neu 2 server va LB cung may thi dung `start-local-with-lb.ps1 -ClientCount 0 -StartNgrok -StopExisting`; khong can Tailscale. Internet LB chi public TCP bang `ngrok tcp 9000`, khong dung UDP; client `-InternetNgrok` tu tat `LOAD_BALANCER_UDP_PROXY` de cursor dung TCP fallback. Da fix login treo qua LB/ngrok bang async connect trong `LoginForm`, timeout TCP/TLS `CLIENT_CONNECT_TIMEOUT_MS` trong `ClientNetwork`, timeout ROUTE trong `LoadBalancerRouteClient`, safe invoke trong `NetworkEvents`, UI marshal trong `LobbyForm`, client exception log `logs/client_log.txt`, va tat direct fallback sai huong qua ngrok mac dinh. Bo sung kich ban Internet direct khong qua LB: server `start-server.ps1 -ServerId server-1 -StartNgrok`, client `start-client.ps1 -Mode Direct -InternetNgrok -Host <ngrok-host> -TcpPort <ngrok-port>`, script set `CLIENT_FORCE_TCP_REALTIME=1`. Da build/package lai `setup/apps`; test pass 20 skip 1. Van giu `âš ï¸` cho den khi user demo that 3 client/2 server/1 LB/ngrok/DigitalOcean va/hoac direct Internet. | `setup/*.ps1`, `setup/.env.example`, `setup/README.md`, `setup/CHECKLIST.md`, `DrawingClient/Program.cs`, `DrawingClient/Forms/LoginForm.cs`, `DrawingClient/Forms/LobbyForm.cs`, `DrawingClient/Network/NetworkEvents.cs`, `DrawingClient/Network/ClientNetwork.cs`, `DrawingClient/Network/LoadBalancerRouteClient.cs`, `LoadBalancer/LoadBalancer.cs` |
| âš ï¸ | cross-server sync fallback | Co LISTEN/NOTIFY room events, nhung chi nen fallback; realtime chinh can room-affinity dung owner server. | `DrawingServer/Services/CrossServerSyncService.cs` |
| âœ… | don schema Neon | Da audit cloud Neon ngay 2026-05-25 va drop cac bang rong/khong con active code dung: `RoomMembers`, `StickyNotes`, `StickyNoteReplies`, `Stickers`, `ClientRateLimits`, `GifExports`. Fresh schema/migration da cap nhat de khong tao lai cac bang nay. | `DrawingServer/Services/Database/Migrations/004_drop_unused_tables.sql`, `DrawingServer/database_setup.sql` |

## Nhom F: AI

| trang thai | ten tinh nang | ket luan hien tai | file lien quan |
| --- | --- | --- | --- |
| âœ… | tao anh tu van ban | Code/UI da chuyen sang Hugging Face Stable Diffusion. `.env`/`.env.example` dung `HF_TOKEN` va `HF_IMAGE_MODEL=stabilityai/stable-diffusion-xl-base-1.0`; client goi `StabilityAiClient.GenerateImageAsync`, POST den Hugging Face Routing `https://router.huggingface.co/nscale/v1/images/generations` voi `response_format="b64_json"`, `prompt`, `model`, parse `data[0].b64_json`, chen anh vao canvas nhu import image va sync/replay bang `AI_TEXT_TO_IMAGE`. Server luu metadata `provider="huggingface"` va model theo `HF_IMAGE_MODEL`. Build/test pass; kiem thu API that ngay 2026-05-25 bang C# client pass, tao PNG 1024x1024 tai `local\tmp_ai_test\hf-csharp-client-test.png`. | `DrawingClient/AI/StabilityAiClient.cs`, `SharedLib/AI/ApiConfig.cs`, `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `.env` |
| âœ… | xoa nen tu dong | Hoan thien code/UI va user da xac nhan hoat dong: neu dang click chon image object tren canvas thi Remove.bg xoa nen va cap nhat lai dung object do bang cung `ActionID`; neu chua chon image thi chon file, xoa nen voi output PNG, tu chen ket qua giua canvas. Sync qua `AI_BG_REMOVED`, replay nhu `ImportImage`, luu `AiResults`/`DrawHistory`. | `DrawingClient/AI/RemoveBgClient.cs`, `DrawingClient/Drawing/CanvasManager.cs`, `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Network/SecureTcpServer.cs` |

## Quyet dinh da chot ngay 2026-05-25

1. `Khoa vung ve`: da bo khoi scope ngay 2026-05-26; khong con UI/protocol/payload/handler claim area.
2. `Che do quan sat`: bo reaction, observer uu tien chi xem va chat.
3. `Undo/redo`: moi user chi undo/redo action cua chinh minh; da hoan thien theo `ActionID`/`Username`.
4. `GIF export`: da bo khoi scope hien tai theo yeu cau moi; khong can UI, protocol, service hay schema lien quan.
5. `Pixel art`: tich hop vao canvas, grid 64x64.
6. `Watermark`: bo watermark khi export va bo noi dung watermark.
7. `Sticker rotate`: bo khoi scope hien tai, khong can UI xoay.
8. `Canvas size`: canvas co dinh hoan toan trong moi room; moi user chi zoom in/out theo y muon.
9. `Snapshot tu dong/UI/backend`: da bo khoi scope ngay 2026-05-26; khong con service/protocol/DB API/bang `Snapshots`.
10. `Sticky note reply`: bo khoi scope hien tai, chi giu sticky note co ban.
