# 06 - AI Gemini va Remove.bg

Trang thai: `TESTED_BY_CODEX`

## Muc tieu

Hoan thien AI theo yeu cau: Gemini cho AI chinh, Remove.bg cho xoa nen, co fallback ro khi thieu key/quota/timeout.

## Pham vi code

- `DrawingClient/AI/GeminiClient.cs`
- `DrawingClient/AI/RemoveBgClient.cs`
- `SharedLib/AI/ApiConfig.cs`
- `DrawingClient/Forms/MainForm.cs`
- `SharedLib/Payloads/AiPayload.cs`
- `DrawingServer/Network/SecureTcpServer.cs`
- `DrawingServer/Services/Database/DbManager.cs`

## Da co

- Text-to-image da dung Gemini.
- Remove.bg doc key tu `.env`.
- AI result luu `AiResults`.
- Magic erase/image erase da bo.
- Gemini request co semaphore rate limit co ban.
- Cap nhat 2026-05-25: autocomplete/goi y/hoan thien prompt AI da bo khoi scope va da go khoi UI/protocol/code.
- Text-to-image va Remove.bg gui payload AI rieng de server luu `AiResults`, broadcast trong room va luu replay nhu `ImportImage`.
- Loi thieu key, quota/rate limit, timeout va HTTP 5xx duoc doi sang thong bao than thien hon; khong log key.

## Quyet dinh tam thoi

- Autocomplete/goi y/hoan thien prompt AI da bi loai khoi scope.
- Khi AI fail, UI hien MessageBox; thao tac thanh cong dung toast/log nhe.

## Ke hoach trien khai code

1. [x] Bo autocomplete/goi y/hoan thien prompt khoi UI/protocol/code.
2. [x] Giu `GeminiClient.GenerateImageAsync` cho text-to-image.
3. [x] Giu Remove.bg cho xoa nen anh.
4. [x] UI disable button khi dang goi API.
5. [x] Timeout/quota/thieu key co loi than thien.
6. [x] Ket qua chen canvas sync qua server, luu `AiResults` va replay nhu `ImportImage`.
7. [x] Build/test.

## Kiem thu Codex phai chay

- `dotnet build .\NT106_DrawingApp.sln -v:minimal`: pass, 0 warning.
- `dotnet test .\NT106Tests\NT106Tests.csproj -v:minimal`: pass 18, skip 1 trong lan test moi nhat.
- Test khong co key: code path kiem tra `GEMINI_API_KEY`/`REMOVE_BG_API_KEY` truoc khi goi API va hien MessageBox, khong crash.
- Khong log key that; Gemini chi log status/body ngan khi API loi.

## User chay thu

- Co `GEMINI_API_KEY`: text-to-image tao anh va chen canvas.
- Khong co `GEMINI_API_KEY`: hien loi ro.
- Co `REMOVE_BG_API_KEY`: xoa nen anh va tu chen vao canvas.
- Client khac nhan ket qua AI qua room.

## Cap nhat sau khi user xac nhan

- `features.md`: AI text-to-image/remove.bg.
- `status_check.md`: chang AI accepted.
- Ghi nha ky: prompt/test case/ket qua.

## Luu y

- Khong goi API that neu user chua dong y trong chang dang code.
- 1 Gemini key dung chung co nguy co quota, nen can giu rate limit.
