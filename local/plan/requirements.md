# Yeu cau va quyet dinh da chot

Trang thai: `ACTIVE`

File nay chi ghi yeu cau da chot va gia dinh dang dung. Neu phat sinh cau hoi moi trong luc build tung chang, Codex phai hoi user; khi user tra loi, cap nhat vao day.

## AI

- Tat ca AI chinh dung Gemini.
- Chi co 1 `GEMINI_API_KEY` cho cac tinh nang Gemini.
- Remove background dung Remove.bg voi `REMOVE_BG_API_KEY`.
- Text-to-image la tinh nang AI Gemini active; autocomplete/goi y/hoan thien prompt da bo khoi scope.
- Magic erase/image erase khong con trong pham vi.
- AI fail do thieu key/quota/timeout phai bao loi ro va khong treo UI.

## Bao mat va cau hinh

- API key va connection string doc tu `.env`.
- `.env` khong commit.
- `.env.example` co placeholder day du.
- Khong log secret.
- Co the nang package de xu ly vulnerability neu van tuong thich .NET Framework 4.7.2.

## Database

- Neon.tech la database cloud chinh.
- Duoc them migration SQL neu can.
- Bang `AiResults` la bang AI history chinh.
- Migration phai tranh pha du lieu cu neu khong duoc user dong y.

## Network va demo ha tang

- Demo bat buoc: 3 client, 1 load balancer, 2 drawing server, Neon.
- Client internet ket noi public endpoint ngrok cua LB.
- LB la public ingress duy nhat.
- Drawing server khong expose truc tiep ra internet.
- LB -> server dung LAN neu cung mang, Tailscale/MagicDNS neu khac LAN.
- Client demo khong bat buoc cai Tailscale.
- `LOAD_BALANCER_CLIENT_MODE=relay` dung cho public ngrok/internet.
- `LOAD_BALANCER_CLIENT_MODE=direct` chi dung khi client cung LAN/Tailnet voi backend.
- `LOAD_BALANCER_STRATEGY=room-affinity` la mac dinh cho relay: join room route theo `Rooms.owner_server_id`; room moi/khong co owner chon backend it tai hon.
- Realtime relay phai broadcast truoc, luu DB nen sau; Neon/PostgreSQL khong duoc nam tren critical path cua net ve.
- Cross-server sync dung PostgreSQL `LISTEN/NOTIFY`.
- Room limit mac dinh: 5 user/room.

## Quyet dinh tinh nang da chot 2026-05-25

- Khoa vung ve: user giao Codex chon phuong an hop ly. Mac dinh thiet ke nen enforce o server de dam bao dung quyen; client render vung khoa va chan som de UX tot.
- Che do quan sat: bo reaction, observer uu tien chi xem va chat.
- Undo/redo: moi user chi undo/redo action cua chinh minh, khong undo toan canvas cua nguoi khac.
- GIF export: da bo khoi scope ngay 2026-05-25 theo yeu cau user; khong can UI/protocol/service/schema lien quan.
- Pixel art: tich hop vao canvas, grid 64x64.
- Watermark: bo watermark khi export va bo noi dung watermark.
- Sticker rotate: UI keo tha chuot la du, khong can slider/goc so neu khong can thiet.
- Canvas size: canvas co dinh hoan toan trong moi room; moi user co quyen zoom in/out local theo y muon, khong sync canvas size.
- Snapshot: can UI don gian de nguoi dung xem duoc snapshot.

## Cach chap nhan tinh nang

- Codex code va test truoc.
- User chay thu theo huong dan.
- User xac nhan dat thi tinh nang moi duoc danh dau hoan thanh.
- Khi user xac nhan, cap nhat:
  - file chang `.md`
  - `status_check.md`
  - `local/features.md`
  - `11_nhat_ky_thuc_thi_2026-05-24.md`

## Dang can hoi neu cham den

- Khong trien khai autocomplete/goi y/hoan thien prompt AI theo quyet dinh moi.
- Sticker library can asset nao la bat buoc?

## Ap dung workflow cho file nay

- Ke hoach trien khai code: neu mot yeu cau moi lam thay doi code, tao/chon chang tuong ung truoc khi sua.
- Kiem thu Codex: kiem tra yeu cau moi khong mau thuan voi `plan.md` va cac file chang.
- User chay thu: user xac nhan yeu cau/chot cau hoi, khong can chay app rieng cho file nay.
- Cap nhat sau khi user xac nhan: ghi cau tra loi da chot vao file nay va cap nhat chang lien quan.
