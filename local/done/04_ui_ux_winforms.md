# 04 - UI/UX WinForms theo chang nho

Trang thai: `TESTED_BY_CODEX`

## Muc tieu

Lam UI de demo, de thao tac, khong chong control, khong mojibake. Moi thay doi UI la mot chang nho va user phai chay thu.

## Pham vi code

- `DrawingClient/Forms/MainForm.cs`
- `DrawingClient/Forms/LoginForm.cs`
- `DrawingClient/Forms/LobbyForm.cs`
- `DrawingClient/UI/*`
- cac control nho neu tach tu `MainForm`

## Cau hoi can chot

- User muon uu tien panel nao truoc: toolbox, members/chat, AI, gallery, status bar?
- Kich thuoc man hinh demo chinh la bao nhieu?
- Co can giu layout hien tai de tranh rui ro demo gan khong?

## Ke hoach trien khai code

1. Chon mot khu vuc UI duy nhat.
2. Chup/ghi lai hanh vi hien tai neu can.
3. Sua layout/control trong pham vi khu vuc do.
4. Khong doi logic network/drawing neu chi sua UI.
5. Dam bao event subscribe/unsubscribe dung lifecycle.
6. Build va chay client neu co the.

## Kiem thu Codex phai chay

- `dotnet build .\NT106_DrawingApp.sln -v:minimal`
- Neu sua event/network UI: `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal`
- Kiem tra bang doc code: control khong bi duplicate event, co `BeginInvoke` khi nhan network event.

## 2026-05-24 21:49 - Toolbox/MainForm polish

- Khu vuc UI da chon: toolbox va khung phai cua `MainForm`.
- Da sua: them `MinimumSize`, font mac dinh `Segoe UI`, nen panel sang hon, border/padding cho toolbox va panel nguoi dung, chuan hoa anchor/margin/button style trong toolbox.
- Khong doi logic network/drawing va khong them event handler moi.
- Codex test: `dotnet build .\NT106_DrawingApp.sln -v:minimal` pass, 0 warning, 0 error.
- Trang thai: `TESTED_BY_CODEX`, cho user mo client o kich thuoc demo de xem control co chong/tran khong.

## 2026-05-24 21:52 - Toolbox grouping de ve de thao tac hon

- Khu vuc UI da chon: toolbox ben trai cua `MainForm`.
- Da sua: tang rong toolbox, them cac nhom `Ve`, `Lich su`, `Tep va thu vien`, `AI`, `Cong tac`; sap xep lai control thanh cac hang full-width va cap nut lien quan.
- Khong doi logic network/drawing; cac handler click va payload cu giu nguyen.
- Codex test: `dotnet build .\NT106_DrawingApp.sln -v:minimal` pass, 0 warning, 0 error.
- Trang thai: `TESTED_BY_CODEX`, cho user thao tac ve that de danh gia do de tim cong cu.

## User chay thu

- Mo client o kich thuoc demo.
- Login -> Lobby -> Room.
- Kiem tra control khong chong nhau.
- Thu chat/member list/toolbox/AI panel neu co lien quan.

## Cap nhat sau khi user xac nhan

- Cap nhat `features.md` neu UI hoan thien mot tinh nang.
- Ghi vao `status_check.md`: chang UI nao da accepted.

## Luu y

- Khong tach lon `MainForm` trong mot lan neu khong co loi can sua ngay.
- Neu phat hien mojibake trong code/comment, chi sua text hien thi user-facing truoc.
