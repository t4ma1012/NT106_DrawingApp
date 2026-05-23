# TỔNG QUAN DỰ ÁN (NT106 - DRAWING APP)

Đây là tài liệu mô tả chi tiết toàn bộ cấu trúc và quy trình hoạt động của dự án Ứng dụng vẽ trực tuyến nhiều người dùng (Collaborative Drawing App). 

---

## 🏗 1. Cấu trúc thư mục & Nhiệm vụ

Dự án được phân chia thành 4 thành phần (Project) riêng biệt nằm trong Solution chính:

### 1️⃣ `DrawingServer/` (Máy chủ xử lý trung tâm)
Xử lý mọi nghiệp vụ liên quan đến kết nối mạng, đồng bộ dữ liệu và tương tác với cơ sở dữ liệu.
- `Program.cs`: Entry point khởi chạy Server (chạy song song TCP Server và UDP Server).
- `Network/`: Chứa các lớp `SecureTcpServer` và `SecureUdpServer` xử lý gửi/nhận gói tin đa luồng.
- `Services/`: Chứa các dịch vụ xử lý nghiệp vụ cụ thể.
  - `Database/DbManager.cs`: (QUAN TRỌNG) Chứa chuỗi kết nối (Connection String) và xử lý câu lệnh kết nối, truy vấn (PostgreSQL).
  - `RoomService.cs`, `DrawService.cs`: Logic quản lý phòng, đồng bộ nét vẽ.
- `database_setup.sql`: Script gốc để tạo bảng dữ liệu.

### 2️⃣ `LoadBalancer/` (Bộ cân bằng tải)
Ứng dụng trung gian giúp điều hướng người dùng.
- `Program.cs`: Điểm bắt đầu, cấu hình LoadBalancer nghe ở port mặc định (9000).
- `LoadBalancer.cs`: Thuật toán định tuyến (Least Connection - Ít kết nối nhất). Nhận kết nối từ Client và chuyển tiếp (forward) gói tin đến Server đang rảnh nhất.

### 3️⃣ `DrawingClient/` (Ứng dụng người dùng)
Giao diện trực quan cho người dùng tương tác (Sử dụng Windows Forms).
- `Program.cs`: Entry point, khởi chạy `LoginForm` đầu tiên.
- `Forms/`: Chứa giao diện đăng nhập (`LoginForm`), giao diện chính (`MainForm`).
- `Drawing/`: Logic xử lý vẽ trên Canvas (bút, tẩy, màu sắc) và `UndoStack.cs` (Quản lý thu hồi hành động).
- `Network/`: Chứa `ClientNetwork.cs` (Kết nối TCP), `SecureUdpReceiver` (UDP). Chịu trách nhiệm gửi nét vẽ lên Server hoặc LoadBalancer.

### 4️⃣ `SharedLib/` (Thư viện dùng chung)
- `Payloads/`: Định nghĩa các cấu trúc dữ liệu JSON để giao tiếp giữa Client và Server (Ví dụ: `RoomPayload`, `DrawPayload`). Việc dùng chung giúp 2 bên luôn hiểu đúng cấu trúc dữ liệu của nhau.
- `Logging/Logger.cs`: Ghi log hệ thống để dễ dàng theo dõi lỗi.

---

## ⚙️ 2. Phiên bản & Cấu hình môi trường

- **Target Framework:** `.NET Framework 4.7.2`
- **Giao diện:** Windows Forms (WinForms).
- **Cơ sở dữ liệu:** PostgreSQL (Hỗ trợ Local hoặc Cloud như Neon.tech).
- **Giao thức mạng (Network):** Cả TCP (đăng nhập, chat, quản lý phòng) và UDP (truyền toạ độ nét vẽ tốc độ cao).

---

## 📦 3. Các thư viện đi kèm (Dependencies)

Các thư viện bên thứ 3 (NuGet) quan trọng đang sử dụng:
- **`Npgsql`**: Driver chuẩn để C# kết nối và tương tác với PostgreSQL.
- **`Newtonsoft.Json` / `System.Text.Json`**: Dùng để Serialize/Deserialize các gói tin (Payload) sang chuỗi JSON truyền qua môi trường mạng (TCP/UDP).

---

## 🚀 4. Quy trình khởi động hệ thống

Để toàn bộ hệ thống hoạt động trơn tru từ đầu đến cuối, thực hiện theo thứ tự sau:

1. **Chuẩn bị Database**: Đảm bảo Database đã được dựng (Local hoặc Neon.tech) và cập nhật ConnectionString ở `DbManager.cs` (Xem thêm file `Neon_Database_Setup_Guide.md`).
2. **Build Solution**: Nhấn `Ctrl + Shift + B` để biên dịch toàn bộ code mới nhất.
3. **Khởi động DrawingServer**: Chạy `DrawingServer.exe`. Máy chủ sẽ mặc định lắng nghe ở Port **8888**.
4. **Khởi động LoadBalancer (Tuỳ chọn nhưng khuyến nghị)**: Chạy `LoadBalancer.exe`. Ứng dụng sẽ lắng nghe ở Port **9000** và trỏ kết nối tới Server.
5. **Khởi động DrawingClient**: Mở Client, điền IP và Port (`9000` nếu dùng LB, hoặc `8888` nếu dùng Server thẳng) để đăng nhập và trải nghiệm.

---

## 💡 5. Lưu ý quan trọng khi tiếp tục phát triển

1. **Phát triển Payload mới:** Bất kỳ thay đổi cấu trúc gói tin giao tiếp nào, HÃY SỬA trong `SharedLib`. Bạn chỉ cần Build lại `SharedLib` là cả Client và Server đều tự động nhận diện gói tin mới.
2. **Bảo mật Database:** Thông tin chuỗi kết nối trong `DbManager.cs` mang tính riêng tư. Nếu đẩy code lên GitHub Public, hãy thay thế ConnectionString thực tế bằng biến môi trường (Environment.GetEnvironmentVariable) hoặc giấu nó đi trước khi commit.
3. **Cấu hình LoadBalancer:** Ban đầu chỉ cấu hình kết nối tới Server cổng 8888. Nếu mở rộng thêm nhiền cụm Server mới (Ví dụ Port 8890, 8891), cần khai báo thêm (AddServer) trong `LoadBalancer/Program.cs`.
4. **Xử lý bất đồng bộ (Async/Await):** Hệ thống đang sử dụng đa nền tảng mạng (Threading/Task). Khi thay đổi UI WinForms từ một tiến trình mạng phản hồi về, BẮT BUỘC phải dùng `Invoke` hoặc `BeginInvoke` để tránh lỗi cross-thread.
