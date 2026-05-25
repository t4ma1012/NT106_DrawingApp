# 08 - Kiem thu va tieu chi hoan thanh

Trang thai: `ACTIVE`

## Muc tieu

Quy dinh cach Codex test va cach user chay thu cho tung chang nho.

## Kiem thu Codex mac dinh

### Neu chi sua tai lieu

- Khong can build.
- Kiem tra file doc co dung flow, khong mau thuan voi `plan.md`.

### Neu sua client UI

```powershell
dotnet build .\NT106_DrawingApp.sln -v:minimal
```

### Neu sua SharedLib/server/network/database

```powershell
dotnet build .\NT106_DrawingApp.sln -v:minimal
dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal
```

### Neu sua package/security/config

```powershell
dotnet build .\NT106_DrawingApp.sln -v:minimal
dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal
dotnet list .\DrawingServer\DrawingServer.csproj package --vulnerable
```

## Test user toi thieu theo loai tinh nang

### Drawing/collab

- 2 client cung room.
- Client A thao tac.
- Client B thay ket qua.
- Client B reconnect va replay dung.

### AI

- Thieu key: bao loi ro.
- Co key: API tra ket qua.
- Ket qua chen canvas va sync client khac.

### Database

- Tao data moi.
- Reconnect/restart server neu can.
- Data cu khong mat.

### LB/ngrok/multi-server

- 3 client.
- 2 server.
- 1 LB.
- Client public qua ngrok.
- Server down/reconnect.

## Mau bao cao Codex cho user

```md
Da sua:
- ...

Da test:
- command: pass/fail

Ban chay thu:
1. ...
2. ...

Neu dat, hay tra loi: "Dat chang <ten chang>".
Neu loi, gui log/man hinh/hanh vi thuc te.
```

## Dieu kien accepted

- Codex test pass.
- User chay thu pass.
- `status_check.md` cap nhat.
- `features.md` cap nhat neu tinh nang user-facing.
- Nhat ky cap nhat.

## Luu y

- Test skip phai co ly do.
- Khong goi demo that/ngrok/Neon production neu user chua dong y.

## Cau hoi can chot

- User muon command test nao la bat buoc cho moi chang?
- Neu test mat nhieu thoi gian, co duoc tach thanh smoke test va full test khong?

## Ke hoach trien khai code lien quan test

1. Neu tinh nang can test tu dong, them test nho trong `NT106Tests`.
2. Neu test can server that, ghi ro ly do skip va huong dan user chay manual.
3. Khong them test flaky phu thuoc ngrok/Neon neu khong co guard.

## User chay thu

User chay dung checklist trong file chang dang `WAITING_USER`, roi tra loi pass/fail.

## Cap nhat sau khi user xac nhan

Neu user thay checklist thieu, cap nhat file nay va file chang lien quan.
