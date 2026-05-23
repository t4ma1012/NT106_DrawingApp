# Hướng dẫn thiết lập & kết nối Database trực tuyến với Neon.tech

Dự án này sử dụng PostgreSQL. Để tiết kiệm thời gian và không cần cài đặt phần mềm CSDL lên máy cá nhân, dự án khuyến khích sử dụng nền tảng Cloud Database [Neon.tech](https://neon.tech) miễn phí. 

Tài liệu này hướng dẫn cách setup nhanh gọn cho bất kỳ thành viên nào (hoặc người chấm điểm) mới tiếp cận thư mục dự án này.

---

## 🚀 Bước 1: Khởi tạo Project trên Neon.tech

1. Truy cập [Neon.tech](https://neon.tech) và tạo tài khoản (cực kỳ nhanh nếu dùng Google/GitHub).
2. Nhấn **Create a project** với các thông số sau:
   - **Project name:** Ví dụ `NT106_DrawingApp`
   - **Postgres version:** `17` (hoặc mới nhất)
   - **Region:** `AWS Asia Pacific 1 (Singapore)` *(Nên chọn Region này để ping từ Việt Nam kết nối nhanh nhất, hỗ trợ vẽ realtime mượt mà)*.
   - **Enable Neon Auth:** **TẶT (Disable / Nút gạt màu xám)** *(Quan trọng: Dự án của chúng ta đã có tự quản lý User và mã hoá riêng).*
3. Bấm **Create project**.

---

## 🗄️ Bước 2: Tạo các Bảng dữ liệu (Tables)

Do Neon tự động set sẵn database là `neondb` nên bạn KHÔNG cần chạy lệnh tạo database. Bạn chỉ cần nạp các bảng (Tables) vào CSDL thôi.

1. Tại giao diện Web của nền tảng Neon, chuyển sang tùy chọn menu **SQL Editor**.
2. Mở sẵn file \`DrawingServer\database_setup.sql\` bằng VS Code hoặc Notepad.
3. Ở file đó, bạn **bỏ qua vài dòng đầu** (về CREATE DATABASE và `\c`). Chỉ copy toàn bộ nội dung từ dòng có chứa **\`CREATE TABLE IF NOT EXISTS Users...\`** cho đến tận hết file.
4. Dán nội dung vừa copy vào màn hình **SQL Editor** trên Neon.
5. Bấm nút **Run** (Góc dưới bên trái). 
   - *Nếu báo Success thì toàn bộ bảng Users, Rooms, DrawHistory... đã được tạo thành công!*

---

## 🔗 Bước 3: Cấu hình mã nguồn C# để trỏ tới Neon

1. Trở lại trang chủ (Dashboard) của dự án Neon.
2. Tìm khối **Connection string**, thả danh sách ngôn ngữ và chọn **.NET**. 
3. (Khuyến nghị) Bật nút **Connection pooling (màu xanh)** để tối ưu lượng kết nối.
4. Bấm **Copy snippet** toàn bộ chuỗi kết nối.
   > ⚠️ **LƯU Ý QUAN TRỌNG:** Đối với các dự án .NET cũ dùng thư viện Npgsql v4.x, tham số `SSL Mode` sinh ra từ web Neon có thể sẽ gây lỗi.
   > Bạn cần sửa lại đoạn cuối của chuỗi kết nối từ `SSL Mode=VerifyFull;Channel Binding=Require;` thành **`SslMode=Require;Trust Server Certificate=true;`**
   > Ví dụ chuỗi chuẩn sẽ có dạng: 
   > \`"Host=your-db-host;Port=5432;Database=drawingapp;Username=your_user;Password=your_password;"\`

5. Quay lại bộ Code (Visual Studio), mở file theo đường dẫn:  
   👉 \`DrawingServer\Services\Database\DbManager.cs\`
6. Tìm đến dòng số 12:
   \`\`\`csharp
   private static readonly string connString = "Host=127.0.0.1;Port=5432;Database=drawingapp;...";
   \`\`\`
7. **Thay thế toàn bộ chuỗi trong dấu nháy kép \`""\`** thành chuỗi kết nối bạn vừa copy từ Neon.

---

## 🎉 Bước 4: Biên dịch và Hoạt động!

1. Nhấn **Ctrl + Shift + B** để Build lại toàn bộ Solution nhằm cập nhật chuỗi kết nối Database mới.
2. Lần lượt chạy:
   - \`DrawingServer\` (chạy ngầm).
   - \`LoadBalancer\` (nếu port 9000).
   - \`DrawingClient\` (giao diện vẽ nơi Client).
3. Đăng nhập với một tài khoản / mật khẩu bất kỳ (hệ thống hỗ trợ ***Auto-register*** tạo nick tự động). Nếu nó báo "Đăng nhập thành công" hoặc "Tạo tài khoản thành công" tức là Database Online của bạn đã **chạy hoàn hảo!**