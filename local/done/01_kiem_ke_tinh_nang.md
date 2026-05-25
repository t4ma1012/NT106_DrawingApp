# 01 - Kiem ke tinh nang va mapping features.md

Trang thai: `ACCEPTED`

## Muc tieu

Doi soat `local/features.md` voi code that, sau do chia cac tinh nang con thieu thanh chang nho de build rieng. File nay khong tu danh dau hoan thanh tinh nang neu user chua chay thu.

## Pham vi code

Khong sua code neu chi dang kiem ke. Neu phat hien sai lech ro rang trong `features.md`, chi cap nhat sau khi da co bang chung build/test hoac user xac nhan.

## Cau hoi can chot

- Neu mot tinh nang co code nhung chua test that, user muon ghi `WARN` hay giu `FAIL`?
- Cac tinh nang P1 nao bat buoc cho demo cuoi: background image, sticker, sticky note, follow, turn-based, GIF, pixel art? AI autocomplete/goi y prompt da bo khoi scope sau nay.

## Ke hoach trien khai

1. Doc `local/features.md`.
2. Doi chieu voi code bang `rg` theo command/payload/UI handler.
3. Phan loai:
   - `PASS`: da code va da user xac nhan.
   - `WARN`: da code/Codex test nhung user chua xac nhan.
   - `FAIL`: chua code hoac chua co bang chung.
4. Cap nhat backlog chang trong `status_check.md`.
5. Khi user xac nhan mot chang, cap nhat dong tuong ung trong `features.md`.

## Kiem thu Codex phai chay

- `dotnet build .\NT106_DrawingApp.sln -v:minimal` neu co sua code.
- Khong can `dotnet test` neu chi cap nhat tai lieu.

## User chay thu

User khong can chay rieng file nay. User chi xac nhan mapping tinh nang co dung voi muc tieu demo hay khong.

## Cap nhat sau khi user xac nhan

- Cap nhat `local/features.md`.
- Ghi vao `status_check.md` muc "Backlog da chot".

## Luu y

- File `features.md` hien dang co icon Unicode; neu PowerShell bi mojibake thi van giu noi dung goc, khong sua encoding tuy tien.

## Ket qua thuc thi 2026-05-24

Da doi soat `local/features.md` voi code hien tai bang `rg` theo payload, handler TCP/UDP, UI event va service DB.

Quy uoc ap dung trong dot kiem ke nay:

- Giu cac dong PASS san co neu file da ghi dat tu truoc, nhung chua coi la user accepted moi.
- Doi cac dong FAIL sang WARN khi da thay code/payload/handler ro rang nhung chua co user test hoac con thieu polish.
- Giu FAIL khi chua thay du code user-facing dap ung mo ta tinh nang.

Bang chung chinh:

- Sticker: co `StickerPickerControl`, drag de dat kich thuoc, `StickerPayload`, server broadcast/save/replay va `CanvasManager.DrawSticker`; chua co UI xoay rieng.
- Sticky note: co control tren canvas, drag, sua text va sync `STICKY_NOTE`; chua thay flow reply rieng.
- Toast: co `ToastForm.ShowToast` va event join/leave.
- Turn-based: co UI toggle, `TurnBasedPayload`, server state va chan ve neu khong den luot.
- GIF: co request/progress/save file, nhung server hien tra GIF 1x1 mau, chua ket xuat DrawHistory that.
- Pixel art: co payload, UDP draw, DB save/sync, nhung chua thay UI grid/rate-limit user-facing.
- AI text-to-image va Remove.bg: co UI trong `MainForm`, client Gemini/Remove.bg va import ket qua vao canvas; can user test voi key that.
- Follow: co payload/broadcast va apply zoom mot phan; chua thay sync viewport day du.
- Background image: chua thay tinh nang chon anh lam nen rieng; hien co doi mau nen va import anh.

Da cap nhat:

- `local/features.md`: cap nhat cac dong sai lech ro rang sang WARN va sua mo ta/path can thiet.
- `local/plan/status_check.md`: cap nhat chang 01 sang `WAITING_USER` va backlog theo ket qua kiem ke.

Codex test:

- Khong chay build/test vi chi sua tai lieu.

User can xac nhan:

- 2026-05-24: User da chap nhan quy uoc WARN cho tinh nang da co code nhung chua user test/polish.
- 2026-05-24: User xac nhan da hoan thanh buoc 1 va 2.
