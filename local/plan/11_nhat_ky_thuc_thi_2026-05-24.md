# Nhat ky thuc thi 2026-05-24

Trang thai: `ACTIVE`

## Quy tac ghi nhat ky

Moi khi mot chang duoc Codex test hoac user accepted, ghi them muc moi:

- Thoi gian.
- Chang/file plan.
- File code da sua.
- Test Codex da chay.
- Ket qua user chay thu.
- Viec con lai.

## 2026-05-25 - Cap nhat tai lieu local va tong quan

- Da chuyen cac chang da code/test xong `01` den `07` vao `local/done/`.
- Da viet lai `local/description.md` thanh file tri thuc tong quan: kien truc, realtime relay, room-affinity, Npgsql 8.0.9, file quan trong, config, build/test/demo, backlog/rui ro.
- Da cap nhat `local/plan/status_check.md` voi ban do tai lieu local va duong dan moi den `local/done`.
- Da cap nhat `local/plan/plan.md`, `local/plan/requirements.md`, `local/plan/09_rui_ro_va_uu_tien.md`, `local/plan/10_runbook_demo_va_doi_soat.md`, `local/features.md`.
- Trang thai moi nhat: build pass 0 warning; test pass 18, skip 1.

## Da thuc hien truoc do

- Thiet lap `.env` va `EnvLoader`.
- Chuyen Gemini/Remove.bg/Neon/LB config sang env.
- Chuyen text-to-image sang Gemini.
- Remove.bg doc key tu env.
- Bo magic erase/image erase.
- Them migration collaboration/AI/server routing.
- Them heartbeat server.
- Them cross-server sync `LISTEN/NOTIFY`.
- Them member list va chat history 50 message.

## 2026-05-24 - Chang 03 database Neon va migration

- Trang thai truoc: `TODO`.
- Da sua: `DrawingServer/Services/Database/Migrations/001_base_schema.sql`, `DrawingServer/Services/Database/Migrations/002_collaboration_features.sql`, `DrawingServer/Services/Database/Migrations/003_ai_gemini_and_server_routing.sql`, `DrawingServer/Services/Database/DbManager.cs`, `DrawingServer/database_setup.sql`, `local/tmp_neon_migrator/Program.cs`, `local/plan/03_database_neon_va_migration.md`, `local/plan/status_check.md`.
- Da lam: them migration base schema cho Neon trống, dong bo migrator local chay 001 -> 003, va cap nhat Gallery/pgcrypto schema.
- Codex test: `dotnet build .\NT106_DrawingApp.sln -v:minimal`; `dotnet build .\local\tmp_neon_migrator\NeonMigrator.csproj -v:minimal`.
- Ket qua: ca 2 build pass; sau do chay `dotnet run --project .\local\tmp_neon_migrator\NeonMigrator.csproj -v minimal` thanh cong tren Neon that va inspect schema public.
- User test: chua co.
- Ket luan: `TESTED_BY_CODEX`.

## Cap nhat gan nhat - LB relay va build sach warning

Chang: `07_tailscale_load_balancer_multi_server.md`

Da sua code:

- `.env.example`
- `DrawingClient/Forms/LoginForm.cs`
- `DrawingClient/Forms/MainForm.cs`
- `DrawingClient/Network/ClientNetwork.cs`
- `DrawingServer/Network/SecureTcpServer.cs`
- `DrawingServer/Network/SecureUdpServer.cs`
- `DrawingServer/Services/AuthService.cs`
- `DrawingServer/Services/CrossServerSyncService.cs`
- `DrawingServer/Services/Database/DbManager.cs`
- `DrawingServer/Services/RoomService.cs`
- `DrawingServer/DrawingServer.csproj`
- `DrawingServer/packages.config`
- `DrawingServer/App.config`

Da lam:

- Them `LOAD_BALANCER_CLIENT_MODE=relay`.
- Client public/ngrok mac dinh ket noi LB relay, khong nhan endpoint backend private.
- Giu `direct` cho LAN/Tailnet khi can route truc tiep.
- Draw/flood fill/text co TCP fallback trong relay.
- Server TCP xu ly va luu draw/fill/text.
- Nang Npgsql len `8.0.9`.
- Don nullable warning de build solution 0 warning.

Codex da test:

- `dotnet build .\NT106_DrawingApp.sln -v:minimal`: pass, 0 warning.
- `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal`: pass 17, skip 1.
- `dotnet list .\DrawingServer\DrawingServer.csproj package --vulnerable`: khong co vulnerable package.

Trang thai user:

- `WAITING_USER`: can user chay thu LB relay local/ngrok.

## 2026-05-24 - Chot topology demo chang 07 va runbook local

- Trang thai truoc: `TESTED_BY_CODEX`.
- Da sua: `local/plan/07_tailscale_load_balancer_multi_server.md`, `local/plan/status_check.md`, `local/plan/10_runbook_demo_va_doi_soat.md`, `local/description.md`, `local/setup.md`.
- Da lam: chot topology demo 3 client khac mang -> ngrok -> LB -> 2 server; cho phep backend o cung LAN hoac qua Tailscale; them runbook A-Z cho ca hai kich ban demo.
- Codex test: chua chay lai build/test sau khi cap nhat tai lieu.
- User test: chua co.
- Ket luan: `TESTED_BY_CODEX`.

## Mau muc tiep theo

```md
## YYYY-MM-DD HH:mm - Chang <ten>

- Trang thai truoc: ...
- Da sua: ...
- Codex test: ...
- User test: ...
- Ket luan: ACCEPTED/REWORK/BLOCKED
```

## 2026-05-25 - Fix realtime drawing UDP sync

- Trang thai truoc: user bao hai client khong dong bo duoc man hinh ve khi ve chung.
- Da sua: `DrawingClient/Network/ClientNetwork.cs`, `DrawingClient/Network/UdpManager.cs`, `DrawingClient/Forms/MainForm.cs`, `DrawingServer/Network/SecureUdpServer.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `LoadBalancer/LoadBalancer.cs`, `LoadBalancer/Program.cs`, `.env.example`, `local/setup/*.ps1`, `local/setup/setup.md`, `local/description.md`, `local/plan/status_check.md`, `local/plan/11_nhat_ky_thuc_thi_2026-05-24.md`.
- Da lam: client gui burst `UDP_PING` sau join phong, server cap nhat UDP endpoint khi port nguon thay doi, `UDP_PING` khong bi broadcast, fallback TCP chi ap dung cho client chua co UDP endpoint, text tool gui dung command `TEXT`, heartbeat client khong tu timeout qua som, LoadBalancer relay ho tro room-affinity bang `ROUTE room=<roomCode>` va `RELAY server=<server_id>`, va health check LB bat tay TLS thay vi TCP thuan.
- Codex test: `dotnet build .\NT106_DrawingApp.sln -v:minimal`; `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal`.
- Ket qua: build pass 0 warning; test pass 18, skip 1.
- User test: chua co, can dong scenario cu, chay lai scenario 1, mo 2 client cung room va ve pen/eraser/line/text lien tuc de xac nhan realtime.
- Ket luan: `TESTED_BY_CODEX`.

## 2026-05-24 - Chang 06 AI Gemini va Remove.bg

- Trang thai truoc: `TODO`.
- Da sua: `DrawingClient/AI/GeminiClient.cs`, `DrawingClient/AI/RemoveBgClient.cs`, `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Network/ClientNetwork.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `SharedLib/Payloads/AiPayload.cs`, `local/plan/06_ai_gemini_removebg.md`, `local/plan/status_check.md`, `local/features.md`, `local/description.md`, `local/plan/11_nhat_ky_thuc_thi_2026-05-24.md`.
- Da lam ban dau: them Gemini text autocomplete prompt, nut `AI: Goi y prompt`, va payload AI rieng. Cap nhat 2026-05-25: autocomplete/goi y/hoan thien prompt AI da bo khoi scope; hien chi giu text-to-image/remove.bg, server luu `AiResults` va replay canvas nhu `ImportImage`, co loi than thien cho thieu key/quota/timeout.
- Codex test: `dotnet build .\NT106_DrawingApp.sln -v:minimal`; `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal`.
- Ket qua: build pass 0 warning; test pass 17, skip 1; test project co warning NU1702 cu do tham chieu .NET Framework tu net9.
- User test: chua co, can test voi `GEMINI_API_KEY`/`REMOVE_BG_API_KEY` that va 2 client cung room.
- Ket luan: `TESTED_BY_CODEX`.

## 2026-05-24 21:49 - Chang 04 UI/UX WinForms

- Trang thai truoc: `TODO`.
- Da sua: `DrawingClient/Forms/MainForm.cs`, `local/plan/04_ui_ux_winforms.md`, `local/plan/status_check.md`, `local/plan/11_nhat_ky_thuc_thi_2026-05-24.md`.
- Da lam: polish nho cho toolbox/MainForm, them `MinimumSize`, font mac dinh, nen panel sang, border/padding va chuan hoa anchor/margin/style cho cac control toolbox.
- Codex test: `dotnet build .\NT106_DrawingApp.sln -v:minimal`.
- Ket qua: build pass, 0 warning, 0 error.
- User test: chua co, can mo client va resize cua so demo de kiem tra control khong chong/tran.
- Ket luan: `TESTED_BY_CODEX`.

## 2026-05-24 21:52 - Chang 04 grouping toolbox DrawingClient

- Trang thai truoc: `TESTED_BY_CODEX`.
- Da sua: `DrawingClient/Forms/MainForm.cs`, `local/plan/04_ui_ux_winforms.md`, `local/plan/status_check.md`, `local/plan/11_nhat_ky_thuc_thi_2026-05-24.md`.
- Da lam: tang rong toolbox va sap xep lai control theo nhom `Ve`, `Lich su`, `Tep va thu vien`, `AI`, `Cong tac`; ghep cac nut lien quan thanh hang doi de giam do dai cuon.
- Khong doi logic network/drawing; cac event handler cu giu nguyen.
- Codex test: `dotnet build .\NT106_DrawingApp.sln -v:minimal`.
- Ket qua: build pass, 0 warning, 0 error.
- User test: chua co, can thao tac ve that de kiem tra do de tim cong cu va do thoang toolbox.
- Ket luan: `TESTED_BY_CODEX`.

## 2026-05-24 - Chang 05 turn-based polish

- Trang thai truoc: `TODO`.
- Da sua: `DrawingClient/Forms/MainForm.cs`, `DrawingClient/Forms/LobbyForm.cs`, `DrawingClient/Network/ClientNetwork.cs`, `DrawingClient/Network/UdpManager.cs`, `DrawingServer/Network/SecureTcpServer.cs`, `DrawingServer/Network/SecureUdpServer.cs`, `DrawingServer/Services/Database/DbManager.cs`, `DrawingServer/Services/RoomService.cs`, `SharedLib/Payloads/RoomPayload.cs`, `local/plan/05_hoan_thien_tinh_nang_ve_va_cong_tac.md`, `local/plan/status_check.md`, `local/features.md`, `local/description.md`, `local/plan/11_nhat_ky_thuc_thi_2026-05-24.md`.
- Da lam: them luong chuyen luot cho chu phong, luon cap nhat `ActiveDrawingUser` tren server, tu chuyen luot khi nguoi dang cam luot roi phong, va cap nhat UI turn panel de hien nut chuyen luot.
- Codex test: `dotnet build .\NT106_DrawingApp.sln -v:minimal`; `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal`.
- Ket qua: build pass 0 warning; test pass 17, skip 1; test project co warning NU1702 ve project reference .NET Framework sang .NET 9.
- User test: chua co, can 2 client cung room de kiem tra owner control va auto-advance khi leave.
- Ket luan: `TESTED_BY_CODEX`.

## 2026-05-24 17:40 - Chang 00/01 guardrail va kiem ke features

- Trang thai truoc: 00 `TODO`, 01 `TODO`.
- Da sua: `local/plan/00_nguyen_tac_va_pham_vi.md`, `local/plan/01_kiem_ke_tinh_nang.md`, `local/plan/status_check.md`, `local/features.md`.
- Ket qua: 00 chuyen `ACTIVE`; 01 chuyen `WAITING_USER`; cac tinh nang co code mot phan duoc danh dau WARN thay vi FAIL.
- Codex test: khong chay build/test vi chi sua tai lieu.
- User test: can user xac nhan quy uoc WARN va chot backlog P1 tiep theo.
- Ket luan: `WAITING_USER`.

## 2026-05-24 - User accepted chang 00/01 va bo sung description workflow

- Trang thai truoc: 00 `ACTIVE`, 01 `WAITING_USER`.
- Da sua: `local/plan/00_nguyen_tac_va_pham_vi.md`, `local/plan/01_kiem_ke_tinh_nang.md`, `local/plan/status_check.md`, `local/features.md`, `local/description.md`, `local/plan/11_nhat_ky_thuc_thi_2026-05-24.md`.
- Ket qua user: chap nhan quy uoc WARN trong `features.md`, xac nhan da hoan thanh buoc 1 va 2.
- Cap nhat workflow: sau moi chang phai bo sung `local/description.md` neu co thong tin kien truc/cau hinh/van hanh/quy uoc phat trien moi.
- Codex test: khong chay build/test vi chi sua tai lieu.
- Ket luan: 01 `ACCEPTED`, 00 tiep tuc `ACTIVE` nhu guardrail chung.

## Cau hoi can chot

- Khi ghi nhat ky, user co muon ghi chi tiet command output hay chi tom tat?

## Ke hoach trien khai code

File nay khong truc tiep trien khai code. Khi mot chang co code moi, ghi lai file da sua va ket qua test sau khi Codex chay.

## Kiem thu Codex phai chay

- Neu chi ghi nhat ky, khong can build.
- Neu nhat ky di kem code change, chay test theo file chang.

## User chay thu

User doc nhat ky de xac nhan chung ta dang ghi dung nhung gi da lam va da test.

## Cap nhat sau khi user xac nhan

Them ket qua user test vao dung chang, kem ket luan `ACCEPTED` hoac `REWORK`.
