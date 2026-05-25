# Collaborative Drawing App

## Quick Config (Latest)

1. Copy `.env.example` to `.env` at repo root.
2. Fill `DATABASE_URL`, `GEMINI_API_KEY`, `REMOVE_BG_API_KEY`.
3. Optional routing:
   - `USE_LOAD_BALANCER_ROUTING=1`
   - configure `LOAD_BALANCER_HOST`, `LOAD_BALANCER_PORT`
4. Room limit default is controlled by `MAX_ROOM_MEMBERS` (current target: `5`).
5. For 2-server demo, use `LoadBalancer/servers.example.json` as template for `servers.json`.

## Current AI Scope

- Enabled: text-to-image (Gemini), remove background (Remove.bg).
- Disabled by scope decision: magic erase.

Đây là ứng dụng Vẽ Trực Tuyến Thời Gian Thực hỗ trợ nhiều người cùng kết nối, vẽ và trò chuyện trong một phòng chung. 

Dự án được xây dựng theo mô hình Client - Server bằng C# (.NET) và sử dụng cơ sở dữ liệu PostgreSQL.

## 🌟 Danh sách tính năng

### 1. Hệ thống Tài khoản & Phòng
- **Tự động Đăng ký/Đăng nhập**: Chỉ cần nhập Username và Password, hệ thống sẽ tự động đăng ký nếu tài khoản chưa tồn tại, hoặc đăng nhập nếu đã có.
- **Bảo mật mật khẩu**: Mật khẩu được băm bằng chuẩn **SHA-256** trước khi lưu vào Database.
- **Tạo và Tham gia phòng**: Người dùng có thể tạo phòng mới (nhận một mã phòng ngẫu nhiên) hoặc tham gia vào phòng có sẵn thông qua mã phòng.
- **Nhật ký hoạt động**: Thông báo thời gian thực khi có người mới tham gia hoặc rời khỏi phòng.

### 2. Công cụ Vẽ (Canvas)
- **Đồng bộ thời gian thực**: Nét vẽ của bạn sẽ ngay lập tức hiển thị trên màn hình của tất cả những người khác trong phòng.
- **Tuỳ chỉnh Bút vẽ**:
  - Chọn màu bút với tính năng hiển thị mã màu khi rọi chuột.
  - Điều chỉnh độ dày nét vẽ với nhãn hiển thị kích thước trực quan.
- **Cục tẩy**: Xoá các nét vẽ lỗi.
- **Hoàn tác & Làm lại (Undo / Redo)**: 
  - Hỗ trợ nút bấm trên giao diện.
  - Hỗ trợ phím tắt tiện lợi: `Ctrl + Z` (Hoàn tác) và `Ctrl + Y` (Làm lại). (Cái này chưa fix được)
- **Công cụ Hút màu**: Rọi chuột vào một điểm bất kỳ trên khung vẽ để lấy màu nền tại điểm đó. (Cái này chưa fix được)
- **Đồng bộ màu nền**: Đổi màu nền của toàn bộ khung vẽ và đồng bộ cho tất cả mọi người trong phòng.
- **Xoá toàn bộ bảng**: Làm sạch khung vẽ với 1 nút bấm.

### 3. Tương tác & Trò chuyện
- **Chat thời gian thực**: Nhắn tin với mọi người trong phòng (tự động ẩn/hiện khung chat).

### 4. Nền tảng Mạng (Networking)
- **Giao thức bảo mật TCP (TLS/SSL)**: Toàn bộ quá trình đăng nhập và điều khiển phòng được mã hoá an toàn.
- **Giao thức UDP**: Truyền tải nét vẽ tốc độ cao, độ trễ thấp giúp trải nghiệm vẽ mượt mà.

---

## 🛠 Hướng dẫn Cài đặt & Setup chi tiết (Cho máy tính mới tinh)

### Bước 1: Cài đặt .NET SDK (Môi trường chạy C#)
Ứng dụng cần .NET SDK để có thể biên dịch và chạy mã C#.
1. Truy cập trang tải chính thức của Microsoft: [Tải .NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
2. Bấm vào nút tải **Windows x64 Installer**.
3. Mở file cài đặt vừa tải về, cứ bấm **Next** và **Install** cho đến khi hoàn tất.

*Nếu bạn dùng Windows 10/11, bạn cũng có thể cài nhanh bằng cách mở PowerShell và dán lệnh sau:*
```powershell
winget install Microsoft.DotNet.SDK.8
```

### Bước 2: Cài đặt Hệ quản trị Cơ sở dữ liệu PostgreSQL
Ứng dụng dùng PostgreSQL để lưu trữ tài khoản người dùng một cách an toàn.
1. Truy cập trang tải chính thức: [Tải PostgreSQL cho Windows](https://www.postgresql.org/download/windows/).
2. Tải bản **Installer** mới nhất và tiến hành cài đặt.
3. Cứ bấm **Next**, nhưng **ĐẶC BIỆT LƯU Ý** ở bước nhập mật khẩu (Password), bạn BẮT BUỘC phải nhập mật khẩu là:
   **`123456`** 
   *(Đây là mật khẩu mà Server của ứng dụng đã được thiết lập để kết nối. Nếu bạn đặt mật khẩu khác, ứng dụng sẽ báo lỗi).*
4. Ở các bước sau (Port, Locale...), cứ để mặc định (Port: `5432`) và bấm Next cho đến khi cài đặt xong.

### Bước 3: Tạo Database (Cơ sở dữ liệu)
1. Bấm nút `Start` trên Windows, tìm kiếm ứng dụng có tên là **pgAdmin 4** (nó vừa được cài kèm với PostgreSQL ở bước trên) và mở nó lên.
2. Nó sẽ yêu cầu bạn nhập mật khẩu. Hãy nhập `123456`.
3. Ở cột bên trái, mở rộng mục **Servers** -> **PostgreSQL...** -> chuột phải vào mục **Databases** -> chọn **Create** -> **Database...**
4. Ở ô **Database**, nhập chính xác chữ: **`drawingapp`** (viết thường, không dấu cách).
5. Bấm **Save**.
*(Bạn không cần tạo các bảng hay cột gì cả, khi chạy Server, code sẽ tự động tạo bảng (Auto-migration) giúp bạn).*

### Bước 4: Chạy Server (Bắt buộc phải chạy Server trước)
Bây giờ mọi thứ đã sẵn sàng. Hãy mở thư mục chứa mã nguồn ứng dụng (ví dụ: `NT106_DrawingApp`).
1. Nhấn chuột phải vào khoảng trống trong thư mục dự án gốc, chọn **Open in Terminal** (hoặc Open PowerShell).
2. Di chuyển vào thư mục Server bằng lệnh:
   ```bash
   cd DrawingServer
   ```
3. Chạy Server bằng lệnh:
   ```bash
   dotnet run
   ```
4. Nếu Terminal in ra dòng chữ màu xanh lá `[SecureTcpServer] Bắt đầu lắng nghe...` nghĩa là máy chủ đã chạy thành công! **(Lưu ý: Bạn KHÔNG được tắt cửa sổ Terminal màu đen này đi, hãy thu nhỏ nó xuống).**

### Bước 5: Chạy Client (Giao diện ứng dụng)
1. Mở lại thư mục chứa mã nguồn.
2. Mở thêm **MỘT CỬA SỔ TERMINAL MỚI** (nhấn chuột phải chọn Open in Terminal).
3. Di chuyển vào thư mục Client bằng lệnh:
   ```bash
   cd DrawingClient
   ```
4. Chạy Client bằng lệnh:
   ```bash
   dotnet run
   ```
5. Giao diện Đăng nhập sẽ hiện lên. Bạn có thể mở ứng dụng thành nhiều cửa sổ (bằng cách lặp lại Bước 5) để thử nghiệm nhiều người dùng cùng vẽ với nhau!

