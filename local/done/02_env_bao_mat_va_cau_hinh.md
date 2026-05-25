# 02 - Env, bao mat va cau hinh

Trang thai: `TESTED_BY_CODEX`

## Muc tieu

Dam bao config/secret/package an toan truoc khi build cac tinh nang khac.

## Pham vi code

- `SharedLib/Config/EnvLoader.cs`
- `SharedLib/AI/ApiConfig.cs`
- `.env.example`
- project/package config
- startup client/server/LB neu can doc env

## Da thuc hien

- App doc cau hinh tu `.env`.
- Gemini, Remove.bg, Neon, LB, server identity, room limit da dua vao env.
- Them `LOAD_BALANCER_CLIENT_MODE=relay`.
- Npgsql nang tu `4.1.12` len `8.0.9`.
- `DrawingServer/App.config` cap nhat binding redirect phu hop.

## Cau hoi can chot

- Neu them bien env moi, ten bien co can theo convention nao cua nhom khong?
- Neu package moi can cai them dependency, user co dong y khong?

## Ke hoach trien khai code khi sua tiep

1. Them bien vao `.env.example`.
2. Doc bang `EnvLoader.Get`/`GetInt`/`GetRequired`.
3. Khong log gia tri secret.
4. Neu config sai/thieu, UI/console phai bao loi ro.
5. Build/test ngay sau thay doi.

## Kiem thu Codex phai chay

```powershell
dotnet build .\NT106_DrawingApp.sln -v:minimal
dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal
dotnet list .\DrawingServer\DrawingServer.csproj package --vulnerable
rg -n "AIza|sk-|DATABASE_URL=.*Password=|REMOVE_BG_API_KEY=.*[A-Za-z0-9]{10}" -g "!*.env" -g "!local/plan/*.md"
```

## User chay thu

- Tao `.env` local tu `.env.example`.
- Chay server/client/LB de xac nhan config doc dung.
- Thu thieu `GEMINI_API_KEY` va `REMOVE_BG_API_KEY` de xem UI bao loi ro.

## Cap nhat sau khi user xac nhan

- `status_check.md`: chuyen muc env/security sang `ACCEPTED`.
- `features.md`: cap nhat nhom bao mat/cau hinh neu co dong lien quan.

## Luu y

- `.env` that khong commit.
- `dotnet test` moi nhat pass 18, skip 1; `NT106Tests` da target `net472` de tranh warning NU1702 cu.
