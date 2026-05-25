# Ke hoach tong quan - build theo chang nho

Trang thai: `ACTIVE`

File nay la file dieu phoi chinh cho toan bo `local/plan`. Tu thoi diem nay, du an khong lam theo kieu sua mot mach dai roi moi test, ma build theo tung chang nho, tung muc tieu ro rang.

## Flow lam viec bat buoc

1. Chon dung 1 chang trong `local/plan`.
2. Doc muc tieu, pham vi code, file lien quan va cau hoi can chot cua chang do.
3. Neu co bat ky diem nao khong chac chan, phai hoi user de chot truoc khi sua code.
4. Sua code trong pham vi chang, khong refactor lan sang chang khac neu khong can.
5. Codex tu chay kiem thu phu hop:
   - `dotnet build .\NT106_DrawingApp.sln -v:minimal`
   - test rieng cua module neu co
   - `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal` khi thay doi logic dung chung/server/network
6. Bao user: da sua gi, da test gi, con rui ro gi, cach user chay thu.
7. Trang thai chang chuyen sang `WAITING_USER`.
8. User chay thu. Neu dat, user tra loi xac nhan.
9. Sau khi user xac nhan dat:
   - cap nhat file chang sang `ACCEPTED`
   - cap nhat `status_check.md`
   - cap nhat `local/features.md`
   - ghi nhat ky vao `11_nhat_ky_thuc_thi_2026-05-24.md`
10. Neu user bao loi, chuyen chang sang `REWORK`, ghi loi va sua tiep trong cung chang.

## Quy uoc trang thai

- `TODO`: chua bat dau.
- `IN_PROGRESS`: Codex dang sua code.
- `CODED`: da sua code, chua test du.
- `TESTED_BY_CODEX`: Codex da build/test pass.
- `WAITING_USER`: dang cho user chay thu.
- `ACCEPTED`: user da xac nhan dat.
- `REWORK`: user bao loi hoac chua dat.
- `BLOCKED`: dang bi chan va can hoi user.

## Yeu cau da chot tu dau

- UI/UX phai de demo, de thao tac, khong mojibake, khong chong control.
- Khong thay doi framework chinh: giu .NET Framework 4.7.2 cho app.
- Secret/API key/connection string doc tu `.env`, khong commit `.env`.
- AI dung Gemini, tru remove background dung Remove.bg.
- Magic erase/image erase da bo khoi pham vi.
- Demo ha tang: 3 client internet -> ngrok -> load balancer -> 2 drawing server -> Neon.
- LB la public ingress duy nhat trong demo internet.
- LB -> server dung LAN neu cung mang, Tailscale/MagicDNS neu khac LAN.
- Client public khong can cai Tailscale.
- Room limit mac dinh: 5 user/room.
- Cross-server sync dung PostgreSQL `LISTEN/NOTIFY`.
- Test tai toi thieu: 10 client dong thoi.

## Thu tu chang nho de thuc thi

1. `00_nguyen_tac_va_pham_vi.md` - nguyen tac, guardrail, cach hoi user.
2. `local/done/01_kiem_ke_tinh_nang.md` - doi soat backlog va mapping voi `features.md`.
3. `local/done/02_env_bao_mat_va_cau_hinh.md` - env, secret, package warning.
4. `local/done/03_database_neon_va_migration.md` - schema, migration, restore data.
5. `local/done/04_ui_ux_winforms.md` - UI demo va tach panel an toan.
6. `local/done/05_hoan_thien_tinh_nang_ve_va_cong_tac.md` - drawing/collab missing features.
7. `local/done/06_ai_gemini_removebg.md` - Gemini text-to-image va Remove.bg; autocomplete/fallback da bo khoi scope.
8. `local/done/07_tailscale_load_balancer_multi_server.md` - LB/ngrok/Tailscale/cross-server.
9. `08_kiem_thu_va_tieu_chi_hoan_thanh.md` - test checklist va acceptance.
10. `09_rui_ro_va_uu_tien.md` - rui ro, rollback, thu tu uu tien.
11. `10_runbook_demo_va_doi_soat.md` - runbook chay thu tung chang va demo tong.
12. `11_nhat_ky_thuc_thi_2026-05-24.md` - nhat ky thuc thi.

## Dieu kien ket thuc moi chang

- Code build pass.
- Test Codex phu hop da chay va ghi lai.
- User co huong dan chay thu ro rang.
- Neu user xac nhan dat, `features.md` va `status_check.md` duoc cap nhat.
- Neu chua dat, chang co muc `Loi user bao lai` va chuyen `REWORK`.

## Ap dung cho file tong quan nay

- Ke hoach trien khai code: file nay khong truc tiep yeu cau sua code; no quy dinh cach chon chang truoc khi code.
- Kiem thu Codex: sau khi sua file nay, kiem tra cac file trong `local/plan` con tuan thu workflow.
- User chay thu: user doc va xac nhan flow lam viec co dung mong muon khong.
- Cap nhat sau khi user xac nhan: neu user doi flow, sua file nay truoc roi moi sua cac file chang.
