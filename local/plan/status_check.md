# Trang thai thuc thi theo chang

Trang thai: `ACTIVE`

File nay la bang dieu khien tien do. Khong ghi chi tiet thiet ke dai o day; chi ghi trang thai, ket qua test, viec cho user xac nhan va loi con lai.

## Quy uoc trang thai

- `TODO`: chua bat dau.
- `IN_PROGRESS`: dang code.
- `TESTED_BY_CODEX`: Codex da test pass, chua user test.
- `WAITING_USER`: dang cho user chay thu.
- `ACCEPTED`: user da xac nhan dat.
- `REWORK`: can sua tiep theo feedback user.
- `BLOCKED`: can hoi user hoac can tai nguyen ben ngoai.

## Bang tien do chang

| Chang | File | Trang thai | Ghi chu |
| --- | --- | --- | --- |
| 00 | `00_nguyen_tac_va_pham_vi.md` | `ACTIVE` | Guardrail chung |
| 01 | `local/done/01_kiem_ke_tinh_nang.md` | `ACCEPTED` | User chap nhan quy uoc WARN va xac nhan hoan thanh buoc 1/2 |
| 02 | `local/done/02_env_bao_mat_va_cau_hinh.md` | `TESTED_BY_CODEX` | Env da co, Npgsql 8.0.9, build 0 warning |
| 03 | `local/done/03_database_neon_va_migration.md` | `ACCEPTED` | Da them base schema migration 001 va da chay thanh cong tren Neon that |
| 04 | `local/done/04_ui_ux_winforms.md` | `TESTED_BY_CODEX` | Da polish va group toolbox/MainForm, cho user chay demo kich thuoc that |
| 05 | `local/done/05_hoan_thien_tinh_nang_ve_va_cong_tac.md` | `TESTED_BY_CODEX` | Turn-based polish co owner control va auto-advance khi leave |
| 06 | `local/done/06_ai_gemini_removebg.md` | `TESTED_BY_CODEX` | Text-to-image da chuyen sang Hugging Face Stable Diffusion va tao anh that thanh cong bang token hien tai; Remove.bg da duoc user xac nhan |
| 07 | `local/done/07_tailscale_load_balancer_multi_server.md` | `TESTED_BY_CODEX` | Room-affinity relay + TCP realtime fast-path; can user demo latency that |
| 08 | `08_kiem_thu_va_tieu_chi_hoan_thanh.md` | `ACTIVE` | Chuan kiem thu |
| 09 | `09_rui_ro_va_uu_tien.md` | `ACTIVE` | Rui ro dang theo doi |
| 10 | `10_runbook_demo_va_doi_soat.md` | `ACTIVE` | Dung khi user chay thu |
| 11 | `11_nhat_ky_thuc_thi_2026-05-24.md` | `ACTIVE` | Nhat ky |

## Ket qua Codex gan nhat

- `dotnet build .\NT106_DrawingApp.sln -v:minimal -p:OutputPath=..\local\tmp_build\Solution\`: pass, 0 warning.
- `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal -p:OutputPath=..\local\tmp_build\Tests\`: pass 17, skip 1, total 18.
- `dotnet list .\DrawingServer\DrawingServer.csproj package --vulnerable`: DrawingServer khong co vulnerable packages.
- `dotnet list .\NT106_DrawingApp.sln package --vulnerable`: khong chay duoc cho ca solution vi mot so project dung `packages.config`.

## Da code gan day, cho user chay thu

### GIF export

Trang thai: `REMOVED`

Da lam:

- User yeu cau loai bo hoan toan ngay 2026-05-25.
- Da go UI `Xuat GIF`, progress/status, client event/network method, shared payload/command, server handler/service/cache, test GIF va schema `GifExports`.
- Chi con tinh nang xuat anh tinh hien co.

## Ban do tai lieu local

| File/thu muc | Trang thai | Muc dich |
| --- | --- | --- |
| `local/description.md` | `ACTIVE` | Tri thuc tong quan moi nhat, doc dau tien trong phien sau |
| `local/features.md` | `ACTIVE` | Bang tinh nang user-facing va trang thai |
| `local/done/01_kiem_ke_tinh_nang.md` | `DONE` | Chang da thuc hien xong |
| `local/done/02_env_bao_mat_va_cau_hinh.md` | `DONE_BY_CODEX` | Da code/test, cho user xac nhan neu can |
| `local/done/03_database_neon_va_migration.md` | `DONE` | Chang da thuc hien xong |
| `local/done/04_ui_ux_winforms.md` | `DONE_BY_CODEX` | Da code/test, cho user demo UI |
| `local/done/05_hoan_thien_tinh_nang_ve_va_cong_tac.md` | `DONE_BY_CODEX` | Da code/test, cho user demo collab |
| `local/done/06_ai_gemini_removebg.md` | `DONE_BY_CODEX` | Da code/test, can key that de user demo |
| `local/done/07_tailscale_load_balancer_multi_server.md` | `DONE_BY_CODEX` | Da code/test, can user demo internet/ngrok |
| `local/plan/00_nguyen_tac_va_pham_vi.md` | `ACTIVE` | Guardrail lam viec |
| `local/plan/08_kiem_thu_va_tieu_chi_hoan_thanh.md` | `ACTIVE` | Checklist test/acceptance |
| `local/plan/09_rui_ro_va_uu_tien.md` | `ACTIVE` | Rui ro va uu tien tiep theo |
| `local/plan/10_runbook_demo_va_doi_soat.md` | `ACTIVE` | Runbook xac nhan chang/demo |
| `local/plan/11_nhat_ky_thuc_thi_2026-05-24.md` | `ACTIVE` | Nhat ky thuc thi |
| `local/plan/plan.md` | `ACTIVE` | Workflow tong quan |
| `local/plan/requirements.md` | `ACTIVE` | Yeu cau/quyet dinh da chot |
| `local/plan/status_check.md` | `ACTIVE` | Bang dieu khien tien do |
| `setup/` | `ACTIVE` | Goi setup duy nhat: exe build san, script role/scenario, README va checklist |

### UI toolbox/MainForm polish

Trang thai: `WAITING_USER`

Da lam:

- Them `MinimumSize` cho `MainForm` de tranh resize qua nho trong demo.
- Dat font mac dinh `Segoe UI`.
- Lam ro toolbox va panel phai bang nen sang, border va padding.
- Chuan hoa anchor/margin/style cho cac control trong toolbox de giam nguy co tran/chong khi panel scroll doc.
- Tang rong toolbox va gom control thanh cac nhom: ve, lich su, tep/thu vien, AI, cong tac.
- Dat cac nut lien quan tren cung mot hang khi hop ly: zoom, undo/redo, export anh, gallery/save, follow.

Codex da test:

- `dotnet build .\NT106_DrawingApp.sln -v:minimal`: pass, 0 warning.

User can chay thu:

- Login -> Lobby -> Room.
- Resize cua so gan kich thuoc demo va nho hon mot chut.
- Kiem tra toolbox ben trai, chat/members/log ben phai, nut an/hien chat va scroll doc toolbox.

### LB relay va TCP fallback

Trang thai: `WAITING_USER`

Da lam:

- Them `LOAD_BALANCER_CLIENT_MODE=relay` vao `.env.example`.
- Login client ho tro 2 mode:
  - `relay`: ket noi TCP vao LB/ngrok, khong nhan endpoint backend private.
  - `direct`: goi `ROUTE` de ket noi truc tiep backend, chi dung LAN/Tailnet.
- Trong mode relay, draw/flood fill/text va cursor gui qua TCP fallback; laser da bo khoi scope.
- Server TCP nhan/lui draw/flood fill/text, broadcast nhanh, luu DB nen va publish cross-server.
- DrawingServer va LoadBalancer dung `Npgsql 8.0.9`.
- Build solution sach warning.

Codex da test:

- Build pass 0 warning.
- Unit test pass 17, skip 1, total 18.
- Vulnerability check DrawingServer pass.

User can chay thu:

- Chay 1 LB + 2 server local voi `LOAD_BALANCER_CLIENT_MODE=relay`.
- Client ket noi vao LB port `9000`.
- Login, tao room, join room bang client thu 2 va client thu 3.
- Thu pen/flood fill/text/chat/member list.
- Neu co ngrok, cap nhat `LOAD_BALANCER_HOST/PORT` theo ngrok va thu lai tu client khac mang.
- Neu 2 backend khac mang, thay host backend trong LB bang IP Tailscale.

Khi user xac nhan dat:

- Cap nhat `local/done/07_tailscale_load_balancer_multi_server.md` sang `ACCEPTED` cho muc relay/TCP fallback.
- Cap nhat `local/features.md` phan load balancer/network.
- Cap nhat `setup/README.md` va `setup/CHECKLIST.md` neu topology demo thay doi.

### Realtime drawing UDP sync

Trang thai: `TESTED_BY_CODEX`

Da lam:

- Client dang ky UDP endpoint bang burst 5 goi `UDP_PING` sau khi join phong thay vi chi gui 1 goi.
- Server cap nhat endpoint moi khi nhan UDP tu client, nen restart/NAT doi port khong lam ket noi realtime bi ket.
- Server xu ly `UDP_PING` nhu goi dang ky noi bo va khong broadcast sang client khac.
- Neu client trong phong chua co UDP endpoint, server gui fallback qua TCP cho client do; khi endpoint da san sang, duong nhanh van la UDP.
- Cursor cap nhat 2026-05-25 theo yeu cau moi: chi giu cursor, bo laser. `MainForm` gui TCP fallback khi `_udpManager` null hoac `PreferTcpRealtime=true`; `ClientNetwork` co send/receive `CURSOR`; `SecureTcpServer` broadcast cursor trong room va khong luu DB; `SecureUdpServer` cung fallback TCP cho cursor voi client chua co UDP endpoint va bo qua `LASER` legacy.
- Cursor cap nhat tiep ngay 2026-05-25 cho direct/LAN va TCP fallback: `MainForm` khong con throttle 35ms trong UI thread; moi `MouseMove` chi cap nhat latest-state co `RoomCode` + `Timestamp`, timer nen flush `CURSOR` moi ~12ms qua UDP/TCP. Ben nhan gom latest cursor theo user va render bang timer UI ~15ms, bo packet cu hon theo `Timestamp` de tranh backlog UI; `UdpManager` cache endpoint server de tranh resolve host moi packet. `SecureUdpServer` bo log moi goi cursor, khong echo pointer ve sender va chuan hoa payload.
- Text tool gui UDP command `TEXT` dung loai lenh thay vi di qua `DRAW`.
- Client heartbeat doi thanh interval 15s, timeout 60s de khong tu ngat TCP sau mot khoang yen lang.
- LoadBalancer relay mac dinh `LOAD_BALANCER_STRATEGY=room-affinity`: join room dung `ROUTE room=<roomCode>` + `RELAY server=<server_id>` de bam owner server; room moi/khong co owner route chon backend it tai hon.
- LoadBalancer da dung SDK-style `PackageReference` voi `Npgsql 8.0.9`, sua loi parse `SslMode`.
- TCP realtime relay broadcast truoc roi luu `DrawHistory` nen sau de giam do tre.
- LB health check bat tay TLS voi DrawingServer, giam log `Authentication failed` do probe TCP thuan.

Codex da test:

- `dotnet build .\NT106_DrawingApp.sln -v:minimal`: pass, 0 warning.
- `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal`: pass 17, skip 1, total 18.

User can chay thu:

- Dong tat ca cua so scenario cu, build lai, roi chay lai `powershell -ExecutionPolicy Bypass -File .\local\setup\scenario-1-local.ps1`.
- Mo 2 client cung room, ve pen/eraser/line/text lien tuc.
- Di chuot tren canvas de xem cursor ten/cham cua client khac. Thu lai trong ca direct/LAN va LB relay/ngrok neu co; laser da bo khoi scope.
- Xac nhan client con lai thay net ve gan realtime va client vao sau van replay duoc history.

### AI Hugging Face Stable Diffusion va Remove.bg

Trang thai: `TESTED_BY_CODEX`

Da lam:

- Da bo toan bo luong hoan thien/goi y prompt: nut UI, `GeminiClient.GenerateTextAsync`, payload/event/command `AI_AUTOCOMPLETE`.
- Text-to-image gui command AI rieng `AI_TEXT_TO_IMAGE`; Remove.bg gui command `AI_BG_REMOVED`.
- Client nhan command AI va render vao canvas nhu anh import.
- Server broadcast ket qua AI cho client khac truoc, roi luu `AiResults` va replay canvas nhu `ImportImage` trong nen de client vao sau thay lai anh AI.
- Cap nhat 2026-05-25: text-to-image da chuyen sang Hugging Face Stable Diffusion. `.env`/`.env.example` dung `HF_TOKEN` va `HF_IMAGE_MODEL=stabilityai/stable-diffusion-xl-base-1.0`.
- `StabilityAiClient` goi Hugging Face Routing `https://router.huggingface.co/nscale/v1/images/generations` voi `response_format="b64_json"`, `prompt`, `model`; parse anh tu `data[0].b64_json`. `MainForm` dung `StabilityAiClient.GenerateImageAsync`; server luu metadata `provider="huggingface"` va model theo `HF_IMAGE_MODEL`.
- Da test API that bang token hien tai: C# client sinh thanh cong `local\tmp_ai_test\hf-csharp-client-test.png` kich thuoc 1024x1024.
- Cap nhat 2026-05-25: Remove.bg neu dang chon image object tren canvas thi xoa nen va update lai dung object do bang cung `ActionID`; neu khong chon image thi van cho chon file va tu chen PNG da xoa nen vao giua canvas.
- User da xac nhan Remove.bg hoat dong on 2026-05-25.
- Hugging Face/Remove.bg co thong bao loi than thien hon cho thieu key, quota/rate limit, billing/credit, timeout va loi server.

Codex da test:

- `dotnet build .\NT106_DrawingApp.sln -v:minimal`: pass, 0 warning.
- `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal`: pass 17, skip 1, total 18.

User can chay thu:

- Khong co `HF_TOKEN`/`REMOVE_BG_API_KEY`: bam nut AI va xac nhan hien loi ro, khong crash.
- Co `HF_TOKEN`: text-to-image tao anh Stable Diffusion va chen giua canvas.
- Co `REMOVE_BG_API_KEY`: test 2 luong, chon image tren canvas roi bam xoa nen de update dung object; va khong chon image thi chon file, xoa nen, tu chen giua canvas.
- Mo 2 client cung room de xac nhan client khac nhan anh AI va client vao sau replay lai duoc.

Khi user xac nhan dat:

- Cap nhat `local/done/06_ai_gemini_removebg.md` va bang tien do sang `ACCEPTED`.
- Cap nhat `features.md` AI tu can user test sang accepted neu demo that pass.

## Viec tiep theo de lam nho

1. Chon anh lam nen va sync/replay.
2. Sticker rotate UI va replay cross-server neu can.
3. Sticky note reply va kiem tra drag/replay.
4. Follow viewport day du: pan + zoom + nguon phat viewport.
5. Turn-based polish: da co next turn/owner control va auto-advance khi leave; cho user test 2 client.
6. Text-to-image Hugging Face da tao anh that thanh cong bang token hien tai; Remove.bg da duoc user xac nhan.
7. Pixel art grid UI + rate limit.
8. Demo 3 client/2 server/1 LB/ngrok/Neon.

## Luu y

- `local/` dang bi `.gitignore`, nen thay doi plan khong hien trong `git status`.
- Test TCP 10 client dang skip vi can server dang chay.
- Trong mode relay TLS, LB khong doc payload `JOIN_ROOM`; room owner routing duoc thuc hien bang preflight `ROUTE room=<roomCode>` va preface `RELAY server=<server_id>`. Cross-server sync van la fallback, khong nen la duong chinh cho net ve lien tuc.

## Ap dung workflow cho file nay

- Ke hoach trien khai code: file nay khong chua code plan chi tiet; no lien ket den file chang dang lam.
- Kiem thu Codex: sau moi chang, ghi dung command da chay va ket qua.
- User chay thu: khi user bao pass/fail, cap nhat trang thai `ACCEPTED` hoac `REWORK`.
- Cap nhat sau khi user xac nhan: dong bo voi file chang, `local/features.md` va `local/description.md` neu co thong tin kien truc/cau hinh/van hanh moi.




