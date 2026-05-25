# 00 - Nguyen tac va pham vi

Trang thai: `ACTIVE`

## Muc tieu cua file

Dat guardrail cho moi chang build nho. Bat ky file ke hoach nao khac cung phai tuan theo file nay.

## Nguyen tac lam viec

- Moi lan chi lam mot chang nho, co muc tieu test duoc.
- Khong sua lan sang module khac neu khong can de hoan thanh chang.
- Neu gap diem chua chac chan, hoi user de chot truoc khi code.
- Khong danh dau hoan thanh neu user chua chay thu va xac nhan.
- Sau moi chang, Codex phai bao:
  - file da sua
  - hanh vi da thay doi
  - lenh test da chay
  - viec user can chay thu
  - rui ro con lai

## Khi nao phai hoi user

- Chua ro UI mong muon, vi tri control, luong thao tac.
- Mot tinh nang co nhieu cach hieu khac nhau.
- Can thay doi database co nguy co mat du lieu.
- Can doi protocol/payload lam anh huong client/server cu.
- Can goi API ton quota/chi phi.
- Can chay lenh phu thuoc may that, ngrok, Tailscale, Neon production.

## Pham vi module

- `DrawingClient`: WinForms, canvas, AI, gallery, chat, network client.
- `DrawingServer`: auth, room, draw sync, database, TCP/UDP, cross-server.
- `LoadBalancer`: ingress, health check, relay/direct routing.
- `SharedLib`: packet, payload, config, security.
- `NT106Tests`: unit/security/load tests.
- `local/features.md`: bang trang thai tinh nang user-facing.
- `local/description.md`: mo ta tong quan du an, kien truc, cau hinh va luu y van hanh can cap nhat sau moi chang co thong tin moi.

## Mau chang nho

Moi file `.md` trong plan phai co cac muc:

- `Trang thai`
- `Muc tieu`
- `Pham vi code`
- `Cau hoi can chot`
- `Ke hoach trien khai code`
- `Kiem thu Codex phai chay`
- `User chay thu`
- `Cap nhat sau khi user xac nhan`
- `Luu y/rui ro`

## Definition of Done cua chang

- `dotnet build .\NT106_DrawingApp.sln -v:minimal` pass.
- Test lien quan pass hoac skip co ly do.
- User da duoc huong dan chay thu.
- User xac nhan dat thi moi cap nhat `ACCEPTED`.
- `local/features.md` phan anh dung tinh nang da dat.
- `local/description.md` duoc bo sung thong tin can thiet neu chang lam thay doi kien truc, cau hinh, luong chay, cach van hanh hoac quy uoc phat trien.

## Xac nhan cua user

- 2026-05-24: User chap nhan guardrail chang 00 va quy uoc trang thai WARN trong `features.md`.
