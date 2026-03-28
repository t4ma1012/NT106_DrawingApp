using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace DrawingServer.Database
{
    public class DbManager
    {
        // THAY ĐỔI Password cho khớp với mật khẩu lúc bạn cài PostgreSQL
        private const string ConnectionString = "Host=localhost;Username=postgres;Password=123456;Database=drawingapp";

        // Hàm băm mật khẩu SHA-256 
        public static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // Xử lý Login
        public static async Task<(bool IsSuccess, string Message)> LoginAsync(string username, string password)
        {
            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                string hash = ComputeSha256Hash(password);

                using var cmd = new NpgsqlCommand("SELECT id FROM Users WHERE username = @u AND password_hash = @p", conn);
                cmd.Parameters.AddWithValue("u", username);
                cmd.Parameters.AddWithValue("p", hash);

                var result = await cmd.ExecuteScalarAsync();

                if (result != null)
                    return (true, "Đăng nhập thành công");
                else
                    return (false, "Sai tài khoản hoặc mật khẩu");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB Error] Login: {ex.Message}");
                return (false, "Lỗi kết nối cơ sở dữ liệu");
            }
        }
        // Hàm Tạo phòng mới (Sinh mã 6 số ngẫu nhiên)
        public static async Task<string> CreateRoomAsync(string username, int width, int height)
        {
            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                // Sinh mã phòng 6 số ngẫu nhiên
                Random rnd = new Random();
                string roomCode = rnd.Next(100000, 999999).ToString();

                using var cmd = new NpgsqlCommand(
                    "INSERT INTO Rooms (room_code, created_by, canvas_width, canvas_height) VALUES (@code, @user, @w, @h) RETURNING room_code", conn);

                cmd.Parameters.AddWithValue("code", roomCode);
                cmd.Parameters.AddWithValue("user", username);
                cmd.Parameters.AddWithValue("w", width);
                cmd.Parameters.AddWithValue("h", height);

                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB Error] CreateRoom: {ex.Message}");
                return "";
            }
        }

        // Hàm Kiểm tra mã phòng có tồn tại không
        public static async Task<bool> CheckRoomExistsAsync(string roomCode)
        {
            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM Rooms WHERE room_code = @code", conn);
                cmd.Parameters.AddWithValue("code", roomCode);

                long count = (long)await cmd.ExecuteScalarAsync();
                return count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB Error] CheckRoomExists: {ex.Message}");
                return false;
            }
        }
    }
}