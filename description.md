# NT106 Drawing App - tri thuc tong quan du an

Trang thai: `ACTIVE`

File nay la entrypoint boi canh cho cac phien lam viec sau. Neu can nam du an nhanh, doc file nay truoc; cac file chi tiet nam trong `local/plan`, `local/done`, `setup` va code theo duong dan ben duoi.

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

- Cap nhat 2026-06-03: da tach ro luong dang nhap va dang ky. `DbManager.LoginAsync` chi xac thuc tai khoan da ton tai va khong con auto-register; `DbManager.RegisterAsync` moi tao user moi trong bang `Users`. `SecureTcpServer` xu ly `LOGIN`/`REGISTER` bang hai ham rieng, nen client direct loopback khong the dang nhap bang tai khoan bat ky chua dang ky.
- Cap nhat 2026-05-26: fix loi kich ban 1 mat ket noi server do `setup/apps/DrawingServer` thieu dependency sau khi `package-release.ps1` bi file lock boi `DrawingServer.exe` dang chay va copy do dang. Da bo sung lai cac DLL thieu tu `DrawingServer/bin/Release/net472` vao `setup/apps/DrawingServer` (bao gom `System.Threading.Tasks.Extensions.dll`) va smoke `start-local-no-lb.ps1 -ClientCount 0 -StopExisting` pass TLS. `setup/package-release.ps1` nay kiem tra process dang chay tu `setup/apps` va fail som voi thong bao ro, tranh xoa/copy nua chung.
- Cap nhat 2026-05-26: giu `CrossServerSyncService` lam fallback, khong tat/gỡ. Da gia co `LoadBalancer` cho `ROUTE room=<roomCode>` trong kich ban 5.3: neu owner trong DB da co nhung health-check stale thi LB ping lai owner ngay truoc khi fail; neu room cu/legacy co `owner_server_id` rong thi LB chon owner on dinh theo hash room, ghi lai `Rooms.owner_server_id`, cache va route toi server do. Neu room khong ton tai trong DB ma LB dang dung, LB log ro `room was not found in LoadBalancer database` de phan biet loi LB/server khac `DATABASE_URL`.
- Cap nhat 2026-05-26: sua loi chat khong tu xuong dong khi tin nhan dai trong `MainForm`. Vung hien thi chat da doi tu `ListBox` sang `RichTextBox` read-only co `WordWrap=true`, tu dong cuon ve cuoi khi co tin moi; chi doi UI hien thi, khong doi logic gui/nhan chat qua network.
- Cap nhat 2026-05-26: fix dong bo net ve khi DB het connection slot (`53300`). Server khong con mo connection DB rieng cho tung packet ve tren critical path; `SecureTcpServer`/`SecureUdpServer` dua draw/flood/text/spray/sticker/background/import/AI image vao `StrokePersistenceQueue`, queue luu tuan tu co retry/backoff va giu pending stroke theo room trong RAM. `DbManager.GetRoomHistoryAsync` ghep them pending stroke chua kip flush DB, nen user vao phong sau van nhan du history hien co trong runtime. `CLEAR_ALL` goi `StrokePersistenceQueue.ClearRoom` de chan cac stroke cu dang queue bi save lai sau khi xoa. `CrossServerSyncService.PublishEventAsync` duoc serialize bang semaphore va `PublishCrossServerEvent` bo qua draw/flood/text/spray tan suat cao de tranh tao connection storm qua PostgreSQL notify. Luu y: khong tu chen `Max Pool Size` vao connection string vi Npgsql ban runtime bao `Couldn't set max pool size`; `PostgresConnectionString.Normalize` chi them `Timeout=15` neu env chua cau hinh.
- Cap nhat 2026-05-26: fix client moi vao phong co history lon nhung canvas trang/bi ngat ket noi. Server `SecureTcpServer` khong con gui toan bo `DrawHistory` trong mot packet raw JSON duy nhat; `SendHistoryToClientAsync` chia `SYNC_BOARD` thanh chunk nho qua `SyncBoardPayload.RawActions` + metadata chunk. Client `ClientNetwork` parse RawActions thanh `DrawAction`, `MainForm` gom cac chunk vao `actionHistory` va chi render khi chunk cuoi den. Gioi han packet TCP client/server tang len 50MB de action anh don le khong lam client tu dong disconnect. `ClientSession.IsDisconnected` duoc danh dau khi write fail de broadcast bo qua session chet, giam spam log `forcibly closed`.
- Cap nhat 2026-05-26: gia co room-affinity cua LoadBalancer. Khi client route vao room cu, LB bat buoc lay dung `Rooms.owner_server_id` hoac cache owner da biet; neu khong lookup duoc owner/owner unhealthy thi tra loi loi thay vi fallback sang server bat ky. Client `ReconnectToRoomOwnerViaLoadBalancerAsync` nay fail join khi khong resolve duoc owner, tranh chia cung mot room sang 2 backend lam mat dong bo. Da build/test pass va `setup/package-release.ps1` cap nhat lai `setup/apps` + `NT106-DrawingApp-setup.zip`.
- Cap nhat 2026-05-26: fix kich ban `setup/start-local-no-lb.ps1` de demo 1 server local khong LB on dinh hon. Script nay bay gio doi server san sang bang TLS handshake that (`Wait-SetupTlsPort`) thay vi TCP socket tho, tranh tao log nhieu dong `Authentication failed because the remote party has closed the transport stream`. Khi mo client trong kich ban local-no-lb, script ep ro `USE_LOAD_BALANCER_ROUTING=0`, `LOAD_BALANCER_CLIENT_MODE=direct`, `SERVER_PUBLIC_HOST=127.0.0.1`, `SERVER_TCP_PORT=8888`, `SERVER_UDP_PORT=8889`, `LOAD_BALANCER_UDP_PROXY=0`, `CLIENT_FORCE_TCP_REALTIME=0`, va truyen `CLIENT_CONNECT_TIMEOUT_MS` tu `.env`/mac dinh 6000. Da smoke test: `start-local-no-lb.ps1 -ClientCount 0 -StopExisting` bao `server-1 san sang TLS`, helper `local/tmp_local_smoke` connect SSL va auth smoke pass tren DigitalOcean schema hien tai.
- Cap nhat 2026-05-26: da chuyen runtime DB sang DigitalOcean PostgreSQL managed. Ca `.env` root va `setup\.env` deu dang tro toi host DigitalOcean `nt106-drawing-db-do-user-37788577-0.m.db.ondigitalocean.com:25060/defaultdb` bang URI `postgresql://...?...sslmode=require` (khong ghi secret vao tai lieu). Da them `SharedLib.Config.PostgresConnectionString.Normalize` va dung trong `DbManager`, `CrossServerSyncService`, `LoadBalancer` de Npgsql nhan duoc URI DigitalOcean/Postgres URI thay vi bao loi `Couldn't set postgresql://...sslmode`. `setup\.env.example` ghi ro chap nhan ca URI DigitalOcean va key-value Npgsql.
- Cap nhat 2026-05-26: user chot khong can copy du lieu ung dung tu Neon sang DigitalOcean, chi can schema/bang giong nhau. Da dung helper schema-only reset `public` tren DigitalOcean va apply migrations `001_base_schema.sql` den `005_drop_snapshots.sql`; doi soat shape Neon va DigitalOcean khop 10 bang active: `actionstack`, `airesults`, `chathistory`, `drawhistory`, `gallery`, `pixelartcells`, `roomevents`, `rooms`, `servernodes`, `users` voi cung so cot tuong ung. Sau schema-only reset, row count tren DigitalOcean deu bang 0.
- Cap nhat 2026-05-26: `local/tmp_neon_migrator` co them `MIGRATOR_ENV_PATH` de chon file env, lenh `schema-only`/`reset-schema` de reset va apply schema khong copy data, va lenh `shape`/`schema-audit` de xem so cot moi bang. `migrate` van la luong copy data day du neu sau nay can.
- Cap nhat 2026-05-26: da bo tinh nang snapshot tu dong va khoa vung ve khoi code/schema. Server khong con start `SnapshotService`, khong con handler `SNAPSHOT_LIST`/`SNAPSHOT_RESTORE`, `SharedLib` khong con payload/command snapshot va claim area, client khong con Shift+right-drag chon vung, `DbManager` khong con API snapshot. Da tao migration `005_drop_snapshots.sql`, go table/index `Snapshots` khoi fresh schema va da apply len Neon online: `Snapshots` hien khong con ton tai. Build pass va test pass 17 skip 1.
- Cap nhat 2026-05-26: ve theo luot da doi quyen chuyen luot. Chu phong van la nguoi bat/tat turn-based, nhung chi user dang giu `ActiveDrawingUser` moi thay/bam duoc nut `Luot ke tiep`; server TCP/UDP enforce `TURN_CHANGE` chi hop le khi `requestedBy == ActiveDrawingUser`.
- Cap nhat 2026-05-26 (dieu chinh theo feedback user): toolbar `MainForm` KHONG tang chieu cao de chua nut. Da doi sang layout compact 3 dong (cao `112px`) theo huong Paint, moi dong co 2 cum trai/phai (`Dock=Fill` + `Dock=Right`) de tan dung khoang trong ben phai va tranh tran ngang. Tool ve duoc rut gon thanh icon nho, cac nhom `Dieu huong`, `Nen`, `Canvas`, `Tep/Luu`, `AI`, `Sticker` duoc dan lai de de thao tac nhanh.
- Cap nhat 2026-05-26: da build lai toan bo solution thanh cong (`dotnet build .\NT106_DrawingApp.sln -v:minimal`) va da chay `setup/package-release.ps1` thanh cong de cap nhat binary Release vao `setup/apps`, dong thoi tao lai goi `NT106-DrawingApp-setup.zip`.
- Cap nhat 2026-05-26: da bo toan bo UI lien quan den follow user trong `MainForm` (o nhap username follow, nut follow, label trang thai follow) va bo subscribe handler `OnFollowModeReceived` tren client UI.
- Cap nhat 2026-05-26: da build lai toan solution va build lai tung app vao `setup\apps` (dung cho `start-local-no-lb.ps1`). Khi client dang mo, file `setup\apps\DrawingClient\SharedLib.dll` se bi lock nen can dong `DrawingClient.exe` truoc khi rebuild runtime binary. Sau khi stop process va build lai, `start-local-no-lb.ps1` da mo ra `DrawingClient.exe`/`DrawingServer.exe` tu `setup\apps`.
- Cap nhat 2026-05-25: da audit Neon cloud bang helper Npgsql. Truoc khi don, schema co cac bang rong/khong con code dung: `clientratelimits`, `gifexports`, `roommembers`, `stickers`, `stickynotes`, `stickynotereplies`. Da them va apply migration `004_drop_unused_tables.sql`; schema cloud sau dot don moi 2026-05-26 con cac bang active: `users`, `rooms`, `drawhistory`, `chathistory`, `gallery`, `actionstack`, `airesults`, `pixelartcells`, `servernodes`, `roomevents`.
- Cap nhat 2026-05-25: setup demo Internet dung ngrok. Internet demo chi public TCP vao LoadBalancer: client -> ngrok TCP -> LB -> 2 server -> Neon; bo UDP trong kich ban Internet va de cursor/tin hieu tam thoi di TCP fallback. UDP proxy LB `9001` van giu cho local/LAN khi can demo noi bo.
- Cap nhat 2026-05-25: `setup/` la goi duy nhat cho moi role. Script load secret tu `setup\.env` (copy tu `setup\.env.example`), khong hoi/truyen `DATABASE_URL` hay server certificate/password qua command line; chi hoi host/IP/port public/LAN/ngrok khi can. Port mac dinh: server-1 TCP/UDP `8888/8889`, server-2 `8890/8891`, LoadBalancer TCP/UDP `9000/9001`. `start-load-balancer.ps1 -StartNgrok` tu mo LB, doi port TCP local san sang, roi chay `ngrok tcp 9000` va in host/port public neu doc duoc API ngrok `127.0.0.1:4040`. `start-client.ps1 -Mode LbRelay -InternetNgrok -Host <ngrok-host> -TcpPort <ngrok-port>` tu tat `LOAD_BALANCER_UDP_PROXY`.
- Cap nhat 2026-05-25: da sua loi PowerShell `Cannot overwrite variable Host` trong `setup/start-client.ps1` bang cach doi bien noi bo thanh `TargetHost` va giu alias `-Host` de lenh cu van dung. Cac script role khong hoi port nua; port lay tu `setup\.env` hoac mac dinh. Khi can host/IP, script chi hoi IP: `start-load-balancer.ps1` hoi `Server-1 host/IP` va `Server-2 host/IP`, co giai thich cach lay bang `ipconfig`/`tailscale ip -4` va default `127.0.0.1` nghia la cung may. `start-server.ps1 -ServerId server-2` nay dung mac dinh `8890/8891` neu `.env` van de `SERVER_ID=server-1`. Da chay `setup/package-release.ps1` de build Release va cap nhat `setup/apps`, sau do smoke test `start-local-no-lb.ps1 -ClientCount 0` va `start-local-with-lb.ps1 -ClientCount 0` deu mo port thanh cong.
- Cap nhat 2026-05-25: fix crash client khi login/join qua LB/ngrok bang cach boc `NetworkEvents` bang safe invoke/log exception, them WinForms unhandled exception logging vao `DrawingClient/Program.cs` (`setup/apps/DrawingClient/logs/client_log.txt`), va sua `LobbyForm` de handler join/create room marshal ve UI thread truoc khi cham control/form. `LoginForm` khong fallback direct nua khi dang o mode LoadBalancer tru khi `CLIENT_ALLOW_DIRECT_FALLBACK=1`, tranh thu ket noi sai `ngrok-host:8888` neu relay loi. `LoadBalancerRouteClient` throw ro neu LB tra `{error}`. `setup/start-local-with-lb.ps1` co them `-StartNgrok`, dung cho topology 2 server + LB cung may; day la kich ban dung neu user chay 2 server tren cung may voi LB, khong can Tailscale. Da build Debug pass, package Release cap nhat `setup/apps`/zip, smoke `start-local-with-lb.ps1 -ClientCount 0 -StopExisting` pass, test pass 17 skip 1.
- Cap nhat 2026-05-25: fix hien tuong client treo tai login khi ket noi Internet qua LB/ngrok bang cach doi `LoginForm` sang ket noi async, them timeout TCP/TLS trong `ClientNetwork` (`CLIENT_CONNECT_TIMEOUT_MS`, mac dinh 6000ms), va them timeout doc ROUTE trong `LoadBalancerRouteClient`. Neu LB/backend/ngrok loi, UI khong bi khoa vo han ma tra ve "Khong the ket noi may chu" va ghi log. Da build Debug pass, test pass 17 skip 1, package Release cap nhat `setup/apps` va `NT106-DrawingApp-setup.zip`.
- Cap nhat 2026-05-25: bo sung kich ban Internet direct khong qua LoadBalancer: server chay `setup/start-server.ps1 -ServerId server-1 -StartNgrok -StopExisting` hoac `ngrok tcp 8888`; client chay `setup/start-client.ps1 -Mode Direct -InternetNgrok -Host <ngrok-host> -TcpPort <ngrok-port>`. Script client direct ngrok set `CLIENT_FORCE_TCP_REALTIME=1`, khong dung UDP vi ngrok TCP khong public UDP.
- Cap nhat 2026-05-25: file `local/plan/13_cau_hoi_dua_du_an_vao_thuc_te.md` da bi huy theo yeu cau user vi khong dung y; cac cau hoi moi phai duoc dat trong trao doi truoc khi lap plan/thuc thi.
- Cap nhat 2026-05-25: client direct mode van doc `SERVER_UDP_PORT` cho LAN/direct. Kich ban Internet moi khong khuyen nghi direct server qua ngrok vi ngrok TCP khong public UDP; dung LB relay + TCP fallback.
- Canvas da chot co dinh 1920x1080 tren client va room; canvas nay tu cover vung nhin thay o muc zoom-out toi da (khong lo khoang chet), khong con combobox chon kich thuoc trong sidebar. Zoom la local, dung `Ctrl + MouseWheel`, `Ctrl +` va `Ctrl -`; co tool `Chuá»™t` de pan viewport bang cach giu chuot trai keo, va khung ben phai co the an/hien tu thanh cong cu trai de mo rong vung ve.
- Cap nhat 2026-05-25: UI `MainForm` da duoc polish lai theo huong ngang gon hon: toolbar chuyen len phia tren, nhom nut ve/nhap/xuat/gallery/AI duoc sap xep thanh cac cum icon, undo/redo va cac loai but dung ky hieu thay cho chu text, khung ben phai thu gon hon va co nut an/hien ngay tren khung chat. Chi thay giao dien, khong doi logic ve/net/AI/chat.
- Cap nhat 2026-05-26: UI `MainForm` da reflow lai thanh cac dong wrap khong scroll (khong dung overflow hay thanh cuon) de khong con tran sang ben phai ngay ca khi full screen; cac nut duoc rut gon con icon ngan, vao tinh than Microsoft Paint toi gian va de thao tac hon. `stickerPicker` van mo dang popup nho, khong chiem chieu cao thanh cong cu.
- Cap nhat 2026-05-26: UI `MainForm` da chuyen sang toolbar phang theo cac dải nong, khong con khung vuong chia group; cac tool van duoc giu day du va duoc sap xep gon thanh cac dải `Vẽ`, `Điều hướng`, `Nền`, `Canvas`, `Tệp/Lưu`, `AI`, `Sticker` de khong mat bat ky cong cu nao. Da bo header strip nho nam phia tren tab `Chat` trong khung ben phai; sidebar chi con tabs `Members/Chat/Nhật ký` + o nhap chat o duoi, logic chat/network khong doi.
- User da test va xac nhan tot luong canvas viewport moi: zoom-out toi da van ve duoc o moi diem dang thay, va nen mau/anh duoc phu dung theo viewport.
- Test gan nhat 2026-05-25: `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal -p:OutputPath=..\local\tmp_build\Tests\` pass 17, skip 1 (`TcpConcurrentConnections_10Clients` can server dang chay), total 18. Lenh test mac dinh bi khoa neu `DrawingClient.exe` dang mo.
- Da xoa cac tinh nang game/challenge da bo: scoring/leaderboard, drawing prompt, blind draw va vote contract; giu lai pixel art nhu tinh nang to mau cong tac theo tung pixel.
- `LoadBalancer` da chuyen sang SDK-style `PackageReference`, dung `Npgsql 8.0.9` giong `DrawingServer`.
- Realtime drawing da chot dung TCP cho draw/flood fill/text o ca relay va direct/LAN de tranh mat net do UDP drop. UDP van la duong nhanh cho cursor/ping/pixel-art trong direct/LAN, cursor co TCP fallback cho LB relay/ngrok hoac client chua co UDP endpoint. Laser da bo khoi scope theo yeu cau moi; server bo qua goi `LASER` legacy. Reaction da duoc user loai khoi scope. Server TCP draw/flood fill/text/spray broadcast ngay, luu `DrawHistory` nen sau, de Neon khong nam tren critical path cua net ve.
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
- Cursor realtime da toi uu lai ngay 2026-05-25: client luu latest-state moi lan `MouseMove`, gan `RoomCode` + `Timestamp`, flush `CURSOR` tu timer nen moi ~12ms qua UDP/AES khi co `_udpManager` va `PreferTcpRealtime=false`, hoac TCP fallback khi relay/ngrok/UDP khong kha dung. Ben nhan khong `BeginInvoke` tung goi nua ma gom latest cursor theo user va render bang timer UI ~15ms, bo qua packet cu hon theo `Timestamp` de tranh backlog lam tre. `UdpManager` cache server endpoint de khong resolve host moi lan gui. Server UDP khong spam log cursor, khong echo ve sender, chuan hoa `Username`/`RoomCode`/`Timestamp`, va chi TCP fallback cho client trong room chua co UDP endpoint. Laser da bo khoi scope; client khong gui/nhan/render laser, server UDP bo qua `LASER` legacy.
- Da cap nhat `local/features.md` ngay 2026-05-25: bang tinh nang bay gio tach `âœ…` chac chan co luong user-facing/code ro, `âš ï¸` co mot phan/can demo that, `âŒ` chua dap ung mo ta, `Bá»Ž` la ngoai pham vi. User da chot: observer chi xem/chat va bo reaction; undo/redo theo action cua chinh minh da lam; GIF export da bo hoan toan khoi scope; pixel art tich hop canvas 64x64; bo watermark; bo sticker rotate UI; bo sticky note replies; canvas size co dinh moi room, chi zoom local; snapshot UI don gian da bo khoi scope theo yeu cau moi.
- GIF export da bi loai bo hoan toan ngay 2026-05-25 theo yeu cau user sau khi tinh nang hoat dong khong on dinh. Khong con UI `Xuat GIF`, progress/status GIF, `ClientNetwork.SendExportGifRequest`, `NetworkEvents.OnGifExportProgress`, shared command `EXPORT_GIF_REQUEST`/`GIF_EXPORT_PROGRESS`, `SharedLib/Payloads/GifExportPayload.cs`, `DrawingServer/Services/GifExportService.cs`, `DrawingServer/Services/DrawHistoryCache.cs`, server handler export GIF, test `NT106Tests/GifExportServiceTests.cs`, hay schema `GifExports` trong migration/setup. Neu sau nay co yeu cau anh dong, phai xem nhu tinh nang moi va hoi user chot lai scope/contract.
- LB relay dang dung `LOAD_BALANCER_STRATEGY=room-affinity`: client join room hoi `ROUTE room=<roomCode>`, LB doc `Rooms.owner_server_id`, client reconnect qua `RELAY server=<server_id>` truoc TLS handshake de join dung backend owner cua room.
- LB room-affinity phai fail closed: neu `ROUTE room=<roomCode>` khong doc duoc `owner_server_id` hoac owner server unhealthy, LB khong duoc fallback random sang backend khac. Client phai dung join va bao loi route de tranh chia cung room qua nhieu server.
- Cac chang 01-07 da code/test boi Codex va duoc chuyen vao `local/done/`; con can user demo that de danh dau accepted neu chua co xac nhan.

## File quan trong can doc/sua

### Client

- `DrawingClient/Forms/LoginForm.cs`: login/register, quyet dinh ket noi direct/relay qua LB, subscribe auth events.
- `DrawingClient/Forms/LoginForm.cs`: khi `USE_LOAD_BALANCER_ROUTING=1`, khong fallback direct neu relay/LB loi tru khi set `CLIENT_ALLOW_DIRECT_FALLBACK=1`; tranh sai huong qua ngrok.
- `DrawingClient/Forms/LoginForm.cs`: login/register connect async de UI khong treo khi ngrok/LB/backend cham; direct mode hien default `SERVER_PUBLIC_HOST`, LB mode hien `LOAD_BALANCER_HOST`.
- `DrawingClient/Forms/LobbyForm.cs`: tao/join room; truoc join room relay se route/reconnect den owner server. Handler join/create room phai marshal ve UI thread truoc khi tao/show/dispose `MainForm` hoac cap nhat control.
- `DrawingClient/Forms/MainForm.cs`: UI chinh, toolbars, AI buttons, chat/members, event handlers realtime, zoom local, toggle an/hien khung phai. Cursor dung latest-state pump flush ~12ms qua UDP hoac TCP fallback; ben nhan gom latest cursor theo user va render timer UI ~15ms de khong bi backlog goi cu.
- `DrawingClient/Forms/MainForm.cs`: UI chinh da chuyen toolbar len tren cung, icon hoa undo/redo va cac tool ve, sap xep lai sticker/import/export/gallery/AI thanh cum gon hon; sidebar phai hinh thanh khung chat/members/logs thu gon va co nut an/hien.
- `DrawingClient/Forms/MainForm.cs`: UI tiep tuc duoc tinh gon theo mau Paint: cac dong toolbar wrap khong scroll thay vi overflow, chia ranh tool / file-AI / sticker-chat de khong tran ngang, icon nut rut gon de de nhan dien, va sticker library mo popup thay vi chen panel lon trong toolbar.
- `DrawingClient/Forms/MainForm.cs`: quan ly lich su action client-side cho undo/redo theo `ActionID`/`Username`; chi undo/redo action gan nhat cua chinh user hien tai va render lai lich su visible.
- `DrawingClient/Drawing/CanvasManager.cs`: ve tren canvas 1920x1080 co dinh, fit viewport, pan/zoom local, mouse events, render draw actions, background mau/anh, image/sticker/text.
- `DrawingClient/Drawing/CanvasManager.cs`: rectangle/circle giu `Shift` khi keo chuot se ep thanh square/perfect circle; pen stroke trong mot lan keo dung chung `ActionID` de undo lui dung 1 stroke.
- `DrawingClient/Drawing/CanvasManager.cs`: quan ly object selection/manipulation cho image/sticker/text, hover cursor resize o goc, va API `DeleteSelectedObject()` de xoa object dang chon.
- `DrawingClient/Drawing/CanvasManager.cs`: resize object da chon bang 4 handle o 4 goc, co tinh toan resize theo tung goc (top-left/top-right/bottom-left/bottom-right).
- `DrawingClient/Drawing/CanvasManager.cs`: render remote cursor theo toa do canvas sau transform zoom/pan; co `UpdateRemoteCursor`, `RemoveRemoteCursor`. Laser render da bi go.
- `DrawingClient/Drawing/TextTool.cs`: TextBox editor tren viewport; commit text ve toa do canvas de sync/replay/manipulation khong lech khi zoom-pan.
- `DrawingClient/Network/ClientNetwork.cs`: TCP/TLS client, heartbeat, reconnect room-owner qua LB, send/receive packet; co `SendCursorRealtime` va nhan `CURSOR` TCP lam fallback cho relay/ngrok.
- `DrawingClient/Network/ClientNetwork.cs`: TCP connect va TLS handshake co timeout `CLIENT_CONNECT_TIMEOUT_MS` (mac dinh 6000ms); `CLIENT_FORCE_TCP_REALTIME=1` bat TCP fallback cho realtime tam thoi trong kich ban direct Internet/ngrok.
- `DrawingClient/Network/LoadBalancerRouteClient.cs`: goi `ROUTE` va `ROUTE room=<roomCode>` den LB.
- `DrawingClient/Network/LoadBalancerRouteClient.cs`: ROUTE co timeout connect/read va throw ro khi LB tra `{error}`.
- `DrawingClient/Network/NetworkEvents.cs`: event hub boc tung subscriber bang try/catch va log exception de event tu network thread khong lam chet app.
- `DrawingClient/Network/UdpManager.cs`: UDP realtime local/direct, `UDP_PING` burst dang ky endpoint co kem `ServerId` cho LB UDP proxy; cursor uu tien UDP khi direct/LAN, con relay/ngrok khong co UDP proxy thi dung TCP fallback trong `ClientNetwork`. Cache server endpoint de tranh resolve host moi packet.
- `DrawingClient/UI/CursorLayer.cs`: hien chi con dung cho emoji/reaction overlay cu; khong con la noi render cursor.
- `DrawingClient/AI/StabilityAiClient.cs`, `RemoveBgClient.cs`: Hugging Face Stable Diffusion text-to-image va remove background. `GeminiClient.cs` da bi go khoi project sau khi chuyen provider tao anh sang Hugging Face.

### Server

- `DrawingServer/Program.cs`: doc env, khoi dong TCP/UDP, heartbeat server node, cross-server sync.
- `DrawingServer/Network/SecureTcpServer.cs`: TCP/TLS protocol, login/room/chat/draw, relay fallback, fast-path broadcast, DB save nen; nhan `CURSOR` TCP fallback de broadcast realtime tam thoi trong room ma khong luu DB.
- `DrawingServer/Network/SecureTcpServer.cs`: voi `IMPORT_IMAGE`, server giu `ActionID` tu payload khi save `DrawHistory` de replay dung luong update object.
- `DrawingServer/Network/SecureTcpServer.cs`: voi `UNDO`/`REDO`, server luu action vao `ActionStack` va khi client join se gui history roi replay undo/redo stack cho client moi.
- `SharedLib/Payloads/DrawPayload.cs`, `InteractionPayload.cs`, `SyncPayload.cs`: bo sung truong `IsDeleted` cho luong text/import-image/sticker de phat lenh xoa object ma khong can them command moi.
- `DrawingClient/Forms/MainForm.cs`: sticky note co luong select/delete rieng; khi nhan packet `STICKY_NOTE` ma `IsOpen=false` thi remove control note neu dang ton tai.
- `DrawingServer/Network/SecureUdpServer.cs`: UDP/AES realtime local/direct, endpoint registration, TCP fallback cho client chua co UDP endpoint, bao gom cursor. Tranh log moi goi cursor, khong echo pointer ve sender, chuan hoa `Username`/`RoomCode`/`Timestamp`, va bo qua `LASER` legacy.
- `DrawingServer/Services/RoomService.cs`: active room state, member list, room owner, turn-based state.
- `DrawingServer/Services/CrossServerSyncService.cs`: PostgreSQL `LISTEN/NOTIFY` fallback khi room bi chia server.
- `DrawingServer/Services/ServerNodeHeartbeatService.cs`: upsert `ServerNodes` de theo doi backend.
- `DrawingServer/Services/Database/DbManager.cs`: Neon/PostgreSQL access, rooms, draw history, chat, gallery, AI, pixel art va timeline theo `DrawHistory`.
- `DrawingServer/Services/Database/StrokePersistenceQueue.cs`: hang doi luu `DrawHistory` tuan tu co retry/backoff, giu pending stroke theo room trong RAM de `SYNC_BOARD` cho client join sau khong thieu net khi DB dang het slot/cham flush.
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
- `setup/`: goi setup duy nhat cho moi role; co script hoi thong so, chay exe build san, README va checklist demo local/LAN/internet ngrok/Tailscale.

## Realtime drawing: duong di va diem nghen

Co 2 mode:

- Direct/LAN: client ket noi thang server hoac LB route truc tiep backend. Draw/flood fill/text di TCP reliable; UDP uu tien cho cursor va endpoint registration, co TCP fallback cho client chua co UDP endpoint. Cursor duoc gui theo latest-state moi ~12ms, va ben nhan chi render vi tri moi nhat moi ~15ms, bo qua cac vi tri cu theo `Timestamp` de thay trang thai hien tai thay vi backlog diem qua khu.
- Relay/ngrok qua LB: client ket noi TCP vao public ngrok host/port cua LB. Internet khong dung UDP proxy; cursor/tin hieu tam thoi di TCP fallback. UDP proxy `9001` chi dung cho local/LAN khi client bat `LOAD_BALANCER_UDP_PROXY=1`.

Duong relay hien tai:

1. Client login vao LB relay.
2. Khi join room cu, `LobbyForm` goi `ClientNetwork.ReconnectToRoomOwnerViaLoadBalancerAsync`.
3. Client goi `LoadBalancerRouteClient.ResolveAsync(lbHost, lbPort, roomCode)`.
4. LB doc `Rooms.owner_server_id` trong Neon va tra `serverId`.
5. Client reconnect den LB, gui preface `RELAY server=<serverId>` truoc TLS handshake.
6. LB proxy phien TLS den dung backend.
7. Client login lai nen, gui `JOIN_ROOM`.
8. Khi ve, server `SecureTcpServer` broadcast packet cho client cung room truoc, roi moi luu `DrawHistory` nen.
9. Khi client join sau, history phai di qua chunked `SYNC_BOARD` (`SyncBoardPayload.RawActions`, `ChunkIndex`, `TotalChunks`, `IsFinalChunk`) de tranh packet qua lon lam client disconnect. Client gom chunk trong `MainForm.actionHistory` va render mot lan khi chunk cuoi den.

Diem can tranh:

- Khong de client cung room roi vao 2 backend khac nhau; neu bi chia, net ve phai di qua `CrossServerSyncService`/PostgreSQL notify va se cham.
- Khong `await DbManager.SaveStrokeAsync` truoc broadcast cho net ve realtime.
- Khong goi `DbManager.SaveStrokeAsync` truc tiep cho tung packet ve tan suat cao tren path realtime. Dung `StrokePersistenceQueue.Enqueue`; queue tu retry neu DB bao `53300`/het slot, va `GetRoomHistoryAsync` them pending stroke cho client vao sau.
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
- `CLIENT_CONNECT_TIMEOUT_MS=6000` mac dinh timeout TCP/TLS cua client, tranh login treo vo han khi ngrok/LB/backend khong phan hoi.
- `CLIENT_FORCE_TCP_REALTIME=1` dung cho Internet direct qua ngrok TCP de cursor/tin hieu tam thoi di TCP fallback thay vi UDP.

Server:

- `SERVER_ID=server-1` / `server-2` phai khop `LoadBalancer/servers.json`.
- `SERVER_NAME`, `SERVER_TCP_PORT`, `SERVER_UDP_PORT`, `SERVER_PUBLIC_HOST`.
- `SERVER_CERT_PATH`, `SERVER_CERT_PASSWORD`, `DATABASE_URL` deu nam trong `setup\.env`/process env; setup script khong hoi cac gia tri nay tren man hinh.

AI:

- `HF_TOKEN`, `HF_IMAGE_MODEL` (mac dinh `stabilityai/stable-diffusion-xl-base-1.0`; Hugging Face Routing dang goi nscale image generation endpoint).
- `REMOVE_BG_API_KEY`.

Build va dong bo database:

- `DrawingServer`, `LoadBalancer` va helper migration tam thoi `local/tmp_neon_migrator` dang dung `Npgsql 8.0.9`.
- `local/tmp_neon_migrator` co cac che do: `audit` de dem row count schema cong khai, `shape`/`schema-audit` de doi soat so cot, `schema-only`/`reset-schema` de reset/apply schema khong copy data, va `migrate` de copy du lieu tu DB nguon sang DB dich qua `TARGET_DATABASE_URL`.
- Helper migration chap nhan `DATABASE_URL` dang `key=value` va `postgresql://...`, tu dong chuan hoa `sslmode=require`; co the set `MIGRATOR_ENV_PATH=setup\.env` de audit/apply DigitalOcean thay vi `.env` root.
- `setup/package-release.ps1` build lai binary release vao `setup/apps` va dong goi lai `NT106-DrawingApp-setup.zip`.

## Build, test, demo

Restore:

```powershell
dotnet restore .\NT106_DrawingApp.sln /p:RestorePackagesConfig=true
```

Build:

```powershell
dotnet build .\NT106_DrawingApp.sln -v:minimal
```

Release package:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\package-release.ps1
```

Test:

```powershell
dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal
```

Demo local nhanh:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-local-with-lb.ps1 -StopExisting
```

Demo local 1 server khong LoadBalancer:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-local-no-lb.ps1 -StopExisting
```

Neu chi can smoke server local khong mo client:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-local-no-lb.ps1 -ClientCount 0 -StopExisting
```

Script local-no-lb phai in `server-1 san sang TLS tai 127.0.0.1:8888`; client do script mo se chay direct vao `127.0.0.1:8888`, khong di qua LoadBalancer.

Demo Internet direct server khong qua LoadBalancer:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup\start-server.ps1 -ServerId server-1 -StartNgrok -StopExisting
powershell -ExecutionPolicy Bypass -File .\setup\start-client.ps1 -Mode Direct -InternetNgrok -Host <ngrok-host> -TcpPort <ngrok-port>
```

## Database schema can nho

- `Users`: tai khoan.
- `Rooms`: room metadata, `room_code`, `owner_id`, `owner_server_id`, canvas size, max members.
- `DrawHistory`: stroke replay cho client vao sau.
- `ChatHistory`, `Gallery`, `AiResults`, `PixelArtCells` cho chat, gallery, AI va pixel art.
- `ServerNodes`: heartbeat backend.
- `RoomEvents`: cross-server events va notify payload.
- Cac bang da loai bo khoi schema cloud ngay 2026-05-25 vi rong/khong con active code dung: `RoomMembers`, `StickyNotes`, `StickyNoteReplies`, `Stickers`, `ClientRateLimits`, `GifExports`.

## Tinh nang chinh

- Canvas GDI+, free draw, shape, eraser, color/thickness/background mau hoac anh, zoom local; viewport canvas co dinh 1920x1080, zoom-out toi da se cover khung nhin thay de moi diem dang thay deu ve duoc. Rectangle/Circle giu `Shift` de ve square/perfect circle.
- Flood fill BFS, text tool, import image, sticker, sticky note.
- Chat, member list, user join/leave, sync board replay, undo/redo theo action gan nhat cua chinh user.
- Cursor realtime: UDP local/direct khi co endpoint, TCP fallback qua relay/ngrok; laser va reaction da bo khoi scope.
- Turn-based drawing: chu phong bat/tat, chi user dang giu luot duoc chuyen sang luot ke tiep, server tu chuyen luot khi user dang cam luot roi phong.
- AI Hugging Face Stable Diffusion text-to-image va Remove.bg; server sync/replay ket qua AI. Tinh nang hoan thien/goi y prompt da bi loai khoi scope.
- Gallery/save/export anh tinh khong watermark theo quyet dinh moi; GIF export da bo khoi scope va khong con UI/protocol/service.
- Pixel art la tinh nang to mau cong tac theo tung pixel, user chot tich hop vao canvas voi grid 64x64; hien co payload/server storage, UI/grid/rate-limit chua hoan chinh.

## Backlog va rui ro

- User can demo that 3 client/2 server/LB/ngrok/Neon de xac nhan latency. Internet ngrok chi TCP nen nghiem thu cursor theo TCP fallback; UDP proxy qua LB chi can nghiem thu local/LAN.
- Theo `local/features.md`, cac tinh nang dang `❌` can trien khai theo quyet dinh moi: spectator chi xem/chat, pixel art UI 64x64 tich hop canvas. Undo/redo cua chinh user, Shift-square/circle va turn-based chuyen luot theo active user da lam; GIF export, sticker rotate UI, sticky note replies, snapshot tu dong/UI/backend va khoa vung ve da bo khoi scope.
- Cac tinh nang da bo khoi scope: reaction, watermark export/gallery, thay doi/sync canvas size.
- Cac tinh nang dang `⚠️`/can demo hoac polish: follow viewport pan+zoom, turn-based UX 2 client, AI voi key that, ngrok/Tailscale demo.
- Cross-server sync chi nen la fallback; khong phu hop cho net ve lien tuc neu can latency thap.

## Quyet dinh user da chot 2026-05-25

- Khoa vung ve: da bo khoi scope ngay 2026-05-26; khong con UI/protocol/payload/handler claim area.
- Observer: chi xem va chat; bo reaction.
- Undo/redo: chi action cua chinh user; da implement theo `ActionID`/`Username`, moi lan keo pen la 1 action.
- GIF export: da bo hoan toan khoi scope hien tai; khong con UI, protocol, service, payload, test hay schema lien quan.
- Pixel art: tich hop vao canvas, grid 64x64.
- Watermark: bo hoan toan khi export/gallery.
- Sticker rotate: da bo khoi scope; khong can UI xoay.
- Canvas size: co dinh trong moi room; moi user zoom local theo y muon.
- Snapshot tu dong/UI/backend: da bo khoi scope ngay 2026-05-26; khong con service, protocol, DB API hay bang `Snapshots` tren Neon.

## Quy tac lam viec tiep

- Neu sua protocol, sua `SharedLib` truoc va build ca solution.
- Neu sua realtime, uu tien critical path: broadcast truoc, IO/DB/log nang lam nen sau.
- Canvas size khong con la tuy chon UI; giu 1920x1080, zoom-out toi da cover viewport, pan duoc clamp de khong lo nen xam/ngoai canvas, va chi doi zoom/pan local neu can.
- Neu sua LB/room routing, giu `server_id` dong bo giua `.env`, `servers.json`, `Rooms.owner_server_id`.
- Neu gap yeu cau moi co kha nang doi schema, hoi user truoc khi migration pha du lieu.
- Sau moi thay doi dang ke: build, test, cap nhat `local/description.md`, `local/plan/status_check.md`, va file lien quan.
