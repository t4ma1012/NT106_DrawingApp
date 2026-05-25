# 05 - Hoan thien tinh nang ve va cong tac

Trang thai: `TESTED_BY_CODEX`

## Muc tieu

Hoan thien cac tinh nang user-facing con thieu theo tung chang nho. Khong gom AI va ha tang multi-server.

## Danh sach chang nho de uu tien

1. Background image sync/replay.
2. Sticker library resize/rotate.
3. Sticky note drag + reply.
4. Follow viewport.
5. Turn-based polish.
6. GIF export.
7. Pixel art grid + rate limit.
8. Smart shape.
9. Toast chuan hoa.

## Da lam

- Chang 5 da duoc trien khai va test: them nut chuyen luot cho chu phong, server xoay `ActiveDrawingUser` theo danh sach member va tu chuyen luot neu nguoi dang cam luot roi phong.

## Pham vi code chung

- `DrawingClient/Forms/MainForm.cs`
- `DrawingClient/Drawing/*`
- `DrawingClient/UI/*`
- `DrawingClient/Network/*`
- `DrawingServer/Network/SecureTcpServer.cs`
- `DrawingServer/Network/SecureUdpServer.cs`
- `DrawingServer/Services/*`
- `SharedLib/Payloads/*`
- `SharedLib/Packets/PacketDef.cs`

## Cau hoi can chot truoc moi tinh nang

- Hanh vi user mong muon la gi?
- Tinh nang can sync realtime hay chi replay khi join sau?
- Can luu DB khong?
- Can TCP fallback qua LB relay khong?
- Khi fail, UI nen bao loi the nao?

## Mau ke hoach trien khai mot tinh nang

1. Chot acceptance criteria voi user.
2. Them/sua payload trong `SharedLib` neu can.
3. Sua client UI/Canvas.
4. Sua server broadcast/save/replay.
5. Sua cross-server publish neu tinh nang phai sync qua 2 server.
6. Them test neu logic tach duoc.
7. Build/test.
8. Bao user chay thu.

## Kiem thu Codex phai chay

- `dotnet build .\NT106_DrawingApp.sln -v:minimal`
- `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal` neu sua payload/server/shared logic
- Test doc code:
  - client local apply va remote apply tach ro
  - server broadcast dung exclude/khong exclude
  - replay client vao sau dung

## User chay thu

Moi tinh nang can user test toi thieu:

- 2 client cung room.
- Client A thao tac tinh nang.
- Client B thay ket qua.
- Client B thoat vao lai, ket qua replay dung.
- Neu co LB relay, test qua LB.

## Cap nhat sau khi user xac nhan

- `features.md`: doi tinh nang tu chua dat sang da dat.
- `status_check.md`: ghi chang accepted.
- File nay: them muc "Da accepted" cho tinh nang.

## Luu y

- Neu tinh nang can DB/migration, phai quay sang `03_database_neon_va_migration.md` truoc.
- Neu tinh nang can public internet, phai dam bao co TCP fallback hoac cross-server sync.
