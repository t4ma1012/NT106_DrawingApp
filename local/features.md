### Trang thai tinh nang - cap nhat 2026-05-25

Trang thai: `ACTIVE`

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
| âœ… | dong bo net ve realtime | Draw/flood fill/text di TCP reliable, server broadcast truoc roi luu DB nen. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Network/ClientNetwork.cs`, `DrawingServer/Network/SecureTcpServer.cs` |
| âœ… | con tro thoi gian thuc | Da toi uu ngay 2026-05-25 cho direct/LAN: client lay toa do canvas sau zoom/pan moi lan `MouseMove`, luu latest-state va flush `CURSOR` bang UDP/AES moi ~8ms khi `_udpManager` san sang (`PreferTcpRealtime=false`). Neu relay/ngrok hoac UDP loi thi moi fallback TCP qua `ClientNetwork`. Server UDP khong spam log cursor, khong echo ve sender, va chi TCP fallback cho client chua co UDP endpoint; client bo qua packet cua chinh user va render ten/cham trong `CanvasManager`. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs`, `DrawingClient/Network/UdpManager.cs`, `DrawingClient/Network/ClientNetwork.cs`, `DrawingServer/Network/SecureUdpServer.cs`, `DrawingServer/Network/SecureTcpServer.cs` |
| âœ… | con tro laser | Da sua/kiem tra code ngay 2026-05-25: Alt + mouse gui laser bang toa do canvas; uu tien UDP direct/LAN va co TCP fallback qua relay/ngrok. Ben nhan render laser trong `CanvasManager` cung transform voi canvas nen khong bi `CursorLayer`/canvas paint che mat; nha Alt gui `IsActive=false` qua cung kenh realtime de xoa laser. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs`, `DrawingClient/Network/UdpManager.cs`, `DrawingClient/Network/ClientNetwork.cs`, `DrawingServer/Network/SecureTcpServer.cs` |
| Bá»Ž | bieu tuong cam xuc/reaction | User chot bo reaction; uu tien che do quan sat chi xem va chat. | Can go/vo hieu hoa UI phim so reaction neu polish |
| âŒ | che do quan sat | Da chot observer chi xem va chat. Chua co UI join spectator, server member info/permission va client disable drawing theo spectator. | Can bo sung |
| âŒ | khoa vung ve | User giao Codex chon phuong an hop ly: nen enforce o server, client render/chan som de UX tot. Hien UI co chon vung va gui `CLAIM_AREA`, server chi broadcast. | Can bo sung |
| âœ… | thong bao noi | `ToastForm` dang duoc goi cho nhieu thao tac UI/join/leave. | `DrawingClient/UI/ToastForm.cs`, `DrawingClient/Forms/MainForm.cs` |
| âœ… | hop tro chuyen | Chat realtime qua TCP, luu `ChatHistory`, gui lai 50 tin gan nhat khi join. | `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `DrawingServer/Services/Database/DbManager.cs` |
| âœ… | undo/redo theo action cua chinh minh | Client luu lich su action theo `ActionID`/`Username`; moi lan keo but dung chung mot `ActionID` nen undo chi lui dung 1 stroke/action gan nhat cua chinh user. Server luu `UNDO/REDO` vao `ActionStack` va phat lai stack nay sau `SYNC_BOARD`, nen client vao phong sau van thay dung trang thai da undo/redo. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `DrawingServer/Services/Database/DbManager.cs` |
| âœ… | danh sach thanh vien | Tab Members hien username/role/status/color, server broadcast `ROOM_MEMBERS`. Role spectator hien chua dung vi spectator chua hoan thien. | `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Services/RoomService.cs` |
| âš ï¸ | theo doi goc nhin | Co payload/nut Follow, hien moi ap dung zoom khi target nhan event; chua co pan/viewport source day du. | `DrawingClient/Forms/MainForm.cs`, `SharedLib/Payloads/InteractionPayload.cs` |
| âœ… | ghi chu dan co ban | Co sticky note tren canvas, drag/sua text va sync qua `STICKY_NOTE`; bo sung resize bang grip goc duoi-phai va dong bo `Width/Height`. Co the click de chon sticky note (highlight), nhan `Delete` de xoa sticky note da chon va dong bo xoa qua `IsOpen=false`. | `DrawingClient/Forms/MainForm.cs`, `SharedLib/Payloads/InteractionPayload.cs` |
| Bá»Ž | phan hoi ghi chu | User chot bo tinh nang reply/phan hoi ghi chu khoi scope hien tai; sticky note co ban van giu tao/sua/keo/resize/select/delete. | Khong can trien khai |
| âš ï¸ | ve theo luot | Co owner toggle, active user, next turn va server chan thao tac khi khong den luot; can demo 2 client de xac nhan UX. | `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `DrawingServer/Services/RoomService.cs` |

## Nhom D: nguoi dung, phong, luu tru

| trang thai | ten tinh nang | ket luan hien tai | file lien quan |
| --- | --- | --- | --- |
| âœ… | dang ky va dang nhap | Login/register qua TCP/TLS, password hash SHA256 trong DB. | `DrawingClient/Forms/LoginForm.cs`, `DrawingServer/Services/Database/DbManager.cs` |
| âœ… | tao va tham gia phong | Tao room 6 so, join room, gioi han thanh vien mac dinh. Canvas size duoc chot co dinh. | `DrawingClient/Forms/LobbyForm.cs`, `DrawingServer/Services/RoomService.cs`, `DrawingServer/Network/SecureTcpServer.cs` |
| âœ… | dong bo bang ve cu | Client join nhan `SYNC_BOARD` tu `DrawHistory` va replay actions. | `DrawingClient/Network/ClientNetwork.cs`, `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Network/SecureTcpServer.cs` |
| âœ… | thu vien ban ve/gallery | Luu canvas vao Gallery, lay danh sach, hien thumbnail va tai anh tu Gallery. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Forms/GalleryForm.cs`, `DrawingServer/Services/Database/DbManager.cs` |
| âœ… | xuat anh | Export canvas hien tai ra file anh. User chot khong watermark. | `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Drawing/CanvasManager.cs` |
| Bá»Ž | xuat GIF | User yeu cau loai bo hoan toan tinh nang export GIF ngay 2026-05-25. Da go UI `Xuat GIF`, progress/status, client event/network method, shared payload/command, server handler/service/cache, test GIF va schema `GifExports`. Chi giu xuat anh tinh PNG/JPEG hien co. | Khong can trien khai |
| Bá»Ž | snapshot UI don gian | User yeu cau bo tinh nang nay khoi scope ngay 2026-05-25; khong bo sung UI snapshot. Snapshot backend van giu neu can dung sau nay. | Khong can trien khai |
| âš ï¸ | snapshot backend | Server co `SnapshotService` chup DrawHistory dinh ky va API restore. | `DrawingServer/Services/SnapshotService.cs`, `DrawingServer/Network/SecureTcpServer.cs` |
| âš ï¸ | timeline/time travel | Server co command timeline va lay history den timestamp; chua duoc user uu tien bang snapshot UI don gian. | `DrawingServer/Network/SecureTcpServer.cs`, `SharedLib/Payloads/SyncPayload.cs` |
| âŒ | pixel art 64x64 tich hop canvas | User chot pixel art tich hop vao canvas, grid 64x64. Hien moi co payload/server DB save/sync, chua co UI grid/rate limit user-facing trong client. | Can bo sung |

## Nhom E: bao mat va ha tang

| trang thai | ten tinh nang | ket luan hien tai | file lien quan |
| --- | --- | --- | --- |
| âœ… | TCP/TLS protocol | TCP duoc boc `SslStream`, co packet framing va heartbeat. | `DrawingClient/Network/ClientNetwork.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `SharedLib/Packets/PacketDef.cs` |
| âœ… | UDP/AES cho tin hieu tam thoi | UDP ma hoa AES dung cho cursor/laser/endpoint registration o direct/LAN. Cursor/laser direct/LAN duoc gui theo latest-state pump ~8ms, server bo log/echo pointer tan suat cao de giam latency; TCP chi fallback khi relay/ngrok hoac client chua co UDP endpoint. Draw quan trong da chuyen TCP de tranh mat net. Reaction da bi loai khoi scope. | `DrawingClient/Network/UdpManager.cs`, `DrawingClient/Network/ClientNetwork.cs`, `DrawingServer/Network/SecureUdpServer.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `SharedLib/Security/AesHelper.cs` |
| âœ… | server da luong | Server accept nhieu TCP client, moi client co task/stream lock rieng. | `DrawingServer/Network/SecureTcpServer.cs`, `DrawingServer/Network/ClientSession.cs` |
| âœ… | PostgreSQL/Neon schema | Co tables Users, Rooms, DrawHistory, ChatHistory, Gallery, AI, Snapshot, PixelArt, RoomEvents. | `DrawingServer/Services/Database/DbManager.cs`, `DrawingServer/Services/Database/Migrations/*.sql` |
| âœ… | load balancer room-affinity | LB co health check TLS, ROUTE, RELAY va room owner affinity. | `LoadBalancer/LoadBalancer.cs`, `DrawingClient/Network/LoadBalancerRouteClient.cs` |
| âš ï¸ | demo internet/playit/Tailscale | Da co `role-setup/` cho tung role va `local/plan/12_kich_ban_demo_playit.md`. Playit direct co the giu TCP+UDP end-to-end; playit qua LoadBalancer hien la TCP relay nen cursor/laser fallback TCP. Can user demo that 3 client/2 server/LB/playit/Neon de xac nhan latency. | `role-setup/*.ps1`, `role-setup/README.md`, `local/plan/12_kich_ban_demo_playit.md` |
| âš ï¸ | cross-server sync fallback | Co LISTEN/NOTIFY room events, nhung chi nen fallback; realtime chinh can room-affinity dung owner server. | `DrawingServer/Services/CrossServerSyncService.cs` |
| âœ… | don schema Neon | Da audit cloud Neon ngay 2026-05-25 va drop cac bang rong/khong con active code dung: `RoomMembers`, `StickyNotes`, `StickyNoteReplies`, `Stickers`, `ClientRateLimits`, `GifExports`. Fresh schema/migration da cap nhat de khong tao lai cac bang nay. | `DrawingServer/Services/Database/Migrations/004_drop_unused_tables.sql`, `DrawingServer/database_setup.sql` |

## Nhom F: AI

| trang thai | ten tinh nang | ket luan hien tai | file lien quan |
| --- | --- | --- | --- |
| âœ… | tao anh tu van ban | Code/UI da chuyen sang Hugging Face Stable Diffusion. `.env`/`.env.example` dung `HF_TOKEN` va `HF_IMAGE_MODEL=stabilityai/stable-diffusion-xl-base-1.0`; client goi `StabilityAiClient.GenerateImageAsync`, POST den Hugging Face Routing `https://router.huggingface.co/nscale/v1/images/generations` voi `response_format="b64_json"`, `prompt`, `model`, parse `data[0].b64_json`, chen anh vao canvas nhu import image va sync/replay bang `AI_TEXT_TO_IMAGE`. Server luu metadata `provider="huggingface"` va model theo `HF_IMAGE_MODEL`. Build/test pass; kiem thu API that ngay 2026-05-25 bang C# client pass, tao PNG 1024x1024 tai `local\tmp_ai_test\hf-csharp-client-test.png`. | `DrawingClient/AI/StabilityAiClient.cs`, `SharedLib/AI/ApiConfig.cs`, `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `.env` |
| âœ… | xoa nen tu dong | Hoan thien code/UI va user da xac nhan hoat dong: neu dang click chon image object tren canvas thi Remove.bg xoa nen va cap nhat lai dung object do bang cung `ActionID`; neu chua chon image thi chon file, xoa nen voi output PNG, tu chen ket qua giua canvas. Sync qua `AI_BG_REMOVED`, replay nhu `ImportImage`, luu `AiResults`/`DrawHistory`. | `DrawingClient/AI/RemoveBgClient.cs`, `DrawingClient/Drawing/CanvasManager.cs`, `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Network/SecureTcpServer.cs` |

## Quyet dinh da chot ngay 2026-05-25

1. `Khoa vung ve`: Codex duoc chon phuong an hop ly; mac dinh nen enforce o server de dung quyen, client chi chan som/render de UX tot.
2. `Che do quan sat`: bo reaction, observer uu tien chi xem va chat.
3. `Undo/redo`: moi user chi undo/redo action cua chinh minh; da hoan thien theo `ActionID`/`Username`.
4. `GIF export`: da bo khoi scope hien tai theo yeu cau moi; khong can UI, protocol, service hay schema lien quan.
5. `Pixel art`: tich hop vao canvas, grid 64x64.
6. `Watermark`: bo watermark khi export va bo noi dung watermark.
7. `Sticker rotate`: bo khoi scope hien tai, khong can UI xoay.
8. `Canvas size`: canvas co dinh hoan toan trong moi room; moi user chi zoom in/out theo y muon.
9. `Snapshot UI don gian`: da bo khoi scope hien tai theo yeu cau moi; khong can trien khai UI xem/chon snapshot.
10. `Sticky note reply`: bo khoi scope hien tai, chi giu sticky note co ban.
