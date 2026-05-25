# Cau hoi can chot de dua du an vao thuc te

Trang thai: `ACTIVE`

## San pham va nguoi dung

1. Ai la nhom nguoi dung chinh: lop hoc, nhom ban, workshop, hay public app?
2. Moi room du kien toi da bao nhieu nguoi ve dong thoi?
3. Can tai khoan that hay chi demo username/password noi bo?
4. Co can vai tro admin/host/moderator rieng khong?
5. Observer chi xem/chat da du, hay can quyen xin phep ve tam thoi?
6. Room co can dat mat khau, private invite link, hay public room list?

## Realtime va ha tang

1. Internet production se dung public VPS, Tailscale, playit, hay cloud load balancer?
2. Co bat buoc giu UDP cho cursor/laser tren internet khong, hay TCP fallback chap nhan duoc?
3. Neu can LoadBalancer + UDP, co chap nhan xay UDP proxy/routing rieng cho LB khong?
4. Muc latency chap nhan cho draw, cursor va chat la bao nhieu ms?
5. Can bao nhieu backend server chay dong thoi trong demo/production?
6. Khi room dang ve ma owner server chet, can failover tu dong hay thong bao reconnect?

## Database va du lieu

1. Du lieu room/draw history can giu bao lau?
2. Co can job don `DrawHistory`, `RoomEvents`, `ChatHistory` cu theo TTL khong?
3. Gallery va AI image co can luu lau dai tren Neon hay chuyen sang object storage?
4. Co can export/backup database truoc moi buoi demo khong?
5. Co can xoa du lieu nguoi dung theo yeu cau privacy khong?

## Bao mat

1. Password hash SHA256 hien tai co du cho demo; production co doi sang BCrypt/Argon2 khong?
2. Certificate TLS se la self-signed, private CA, hay cert hop le theo domain?
3. Client co can verify certificate that thay vi accept all cert khong?
4. API key Hugging Face/Remove.bg nam o client co chap nhan duoc khong, hay phai proxy qua server?
5. Co can rate limit AI/draw/chat theo user/IP khong?
6. Co can audit log thao tac quan tri va login khong?

## Tinh nang con thieu

1. Observer mode co phai muc tieu bat buoc truoc demo cuoi khong?
2. Khoa vung ve can enforce theo hinh chu nhat, polygon, hay freeform?
3. Pixel art 64x64 can tich hop nhu tool rieng hay overlay tren canvas hien tai?
4. Follow viewport co can dong bo pan+zoom day du khong?
5. Snapshot backend co giu lai de restore an toan, hay bo han de giam schema?

## Van hanh va dong goi

1. Moi role se tai source repo, zip release, hay installer rieng?
2. May client co du .NET Framework 4.7.2 chua, hay can installer prerequisite?
3. Co can script tao `.env` tu prompt de tranh sua tay khong?
4. Playit endpoint co dung reserved tunnel co dinh hay tunnel tam thoi moi lan demo?
5. GitHub repo se public hay private, va co can GitHub Actions build/test khong?
6. Release artifact can gom nhung gi: exe, scripts, cert, `.env.example`, docs, hay ca source?
