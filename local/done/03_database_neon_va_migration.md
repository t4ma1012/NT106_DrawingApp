# 03 - Database Neon va migration

Trang thai: `ACCEPTED`

## Muc tieu

Moi thay doi database phai duoc lam bang chang nho, co migration rieng, co cach rollback/kiem tra, va khong pha du lieu cu neu user chua dong y.

## Pham vi code

- `DrawingServer/Services/Database/DbManager.cs`
- `DrawingServer/database_setup.sql`
- `DrawingServer/Database/Migrations/*.sql`
- payload/repository lien quan tinh nang can luu DB

## Da co

- Neon la database chinh.
- Da co migration collaboration/AI/routing.
- Da co migration base schema `001_base_schema.sql` cho Neon trong chang nay.
- `AiResults` la bang AI history chinh.
- `RoomEvents`/`DrawHistory` dung cho sync/replay/failover.

## Cau hoi can chot

- Migration co duoc chay tren Neon that ngay trong chang nay khong?
- Neu can them bang/cot, co can giu backward compatibility voi database cu khong?
- Neu can xoa/sua cot, user co dong y backup truoc khong?

## Ke hoach trien khai code

1. Xac dinh tinh nang can DB.
2. Viet migration SQL rieng, tang so thu tu.
3. Cap nhat `database_setup.sql`.
4. Sua `DbManager` bang query co parameter.
5. Them test/unit neu logic parse/query co the test offline.
6. Chay migration local/Neon theo dong y user.
7. Ghi ket qua vao `status_check.md`.

## Ket qua chang hien tai

- Da them `001_base_schema.sql` de khoi tao schema nền cho Neon trống.
- Da dong bo migrator local de chay 001 -> 003 theo thu tu.
- Da cap nhat `database_setup.sql` va `DbManager` de khop voi schema nền.
- Da build pass solution va migrator local.
- Da chay thanh cong tren Neon that va inspect lai `information_schema`.

## Kiem thu Codex phai chay

- `dotnet build .\NT106_DrawingApp.sln -v:minimal`
- `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal` neu co logic DB wrapper test duoc.
- Neu chay Neon that: ghi migration name, thoi gian, output pass/fail.

## User chay thu

- Login/register.
- Tao room.
- Join room.
- Ve/chat/AI theo tinh nang vua them.
- Disconnect/reconnect de xem data restore dung.

## Cap nhat sau khi user xac nhan

- Cap nhat `features.md` dong tinh nang co DB.
- Ghi migration da accepted vao `status_check.md`.
- Ghi nhat ky vao file `11`.

## Luu y/rui ro

- Khong chay migration pha du lieu neu chua hoi user.
- Neon latency co the anh huong sync; can test voi du lieu that.
