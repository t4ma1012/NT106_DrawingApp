# 09 - Rui ro va uu tien

Trang thai: `ACTIVE`

## Uu tien hien tai

1. User test LB relay room-affinity + TCP realtime fast-path da code.
2. Hoan thien background image sync/replay.
3. Hoan thien sticker resize/rotate.
4. Hoan thien sticky note drag/reply.
5. Hoan thien follow viewport.
6. AI autocomplete/goi y prompt da bo khoi scope; chi giu Gemini text-to-image va Remove.bg.
7. Hoan thien pixel art neu con thoi gian. GIF export da bo khoi scope.
8. Demo 3 client/2 server/LB/ngrok/Neon.

## Rui ro lon

### 1. LB relay/room-affinity can demo that

Anh huong:

- Client cung room neu vao sai backend se phai dong bo qua PostgreSQL notify, do tre co the vuot muc 0.3-0.5s.

Giam thieu:

- Da co `ROUTE room=<roomCode>` + `RELAY server=<server_id>` de route theo `Rooms.owner_server_id`.
- LoadBalancer dung `Npgsql 8.0.9`, dong bo voi DrawingServer va parse duoc `SslMode=Require`.
- Server TCP realtime broadcast truoc, luu DB nen sau.
- Can user demo that 3 client/2 server/LB/ngrok/Neon va kiem tra log `ROUTE`/`PROXY`.

Rui ro con lai:

- `SERVER_ID`, `servers.json.server_id` va `Rooms.owner_server_id` khong khop.
- `DATABASE_URL` cua LoadBalancer sai/thieu lam owner lookup fallback ve least-load.
- Cross-server sync van chi nen la fallback, khong nen la duong chinh cho net ve lien tuc.

### 2. UDP khong qua ngrok TCP

Anh huong:

- Cursor/laser realtime co the khong chay qua public tunnel TCP.

Giam thieu:

- Draw/flood fill/text da co TCP fallback.
- Neu can cursor/laser public, them TCP fallback rieng trong chang moi.

### 3. Database migration pha du lieu

Giam thieu:

- Hoi user truoc khi chay migration that.
- Migration rieng, co backup/rollback note.

### 4. AI quota/key

Giam thieu:

- Rate limit.
- Timeout.
- Loi than thien.
- Khong auto goi API that khi chua can.

### 5. UI MainForm lon

Giam thieu:

- Tach tung panel nho.
- Test event lifecycle sau moi chang.

## Rollback theo chang

- Moi chang phai co danh sach file da sua.
- Neu user bao loi, chi sua trong chang do.
- Khong revert thay doi ngoai pham vi neu khong duoc user yeu cau.

## Cau hoi can chot

- Rui ro nao user chap nhan cho demo, rui ro nao bat buoc xu ly truoc?
- Neu can trade-off giua hoan thien tinh nang va on dinh demo, user uu tien ben nao?

## Ke hoach trien khai code khi xu ly rui ro

1. Chon mot rui ro.
2. Xac dinh chang lien quan.
3. Neu cach giam thieu co nhieu phuong an, hoi user.
4. Sua code nho nhat de giam rui ro.
5. Test theo `08`.

## Kiem thu Codex phai chay

- Theo file chang lien quan.
- Neu rui ro la package/security, chay them vulnerability scan.

## User chay thu

User xac nhan rui ro da duoc giam trong kich ban that, vi co nhieu rui ro chi xuat hien khi demo.

## Cap nhat sau khi user xac nhan

- Chuyen rui ro sang "da giam" hoac xoa khoi P0.
- Cap nhat `status_check.md`.
