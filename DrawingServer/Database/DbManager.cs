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
        // Hàm 1: Lưu nét vẽ vào Database (DrawHistory)
        public static async Task<bool> SaveStrokeAsync(string roomCode, string actionId, string strokeDataJson, string username)
        {
            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                // Dùng Subquery để tự động tìm room_id từ roomCode, lưu chuỗi JSON vào cột stroke_data
                string sql = @"
                    INSERT INTO DrawHistory (room_id, action_id, stroke_data, username) 
                    VALUES ((SELECT id FROM Rooms WHERE room_code = @code), @actionId, @strokeData::jsonb, @user)";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("code", roomCode);

                // Parse chuỗi actionId thành UUID, nếu không có thì tự tạo mới
                cmd.Parameters.AddWithValue("actionId", Guid.TryParse(actionId, out Guid guid) ? guid : Guid.NewGuid());
                cmd.Parameters.AddWithValue("strokeData", strokeDataJson);
                cmd.Parameters.AddWithValue("user", username);

                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB Error] SaveStroke: {ex.Message}");
                return false;
            }
        }

        // Hàm 2: Lấy toàn bộ lịch sử vẽ của một phòng để Đồng bộ (Sync)
        public static async Task<System.Collections.Generic.List<string>> GetRoomHistoryAsync(string roomCode)
        {
            var history = new System.Collections.Generic.List<string>();
            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                // Lấy tất cả nét vẽ của phòng đó, sắp xếp theo thời gian cũ -> mới
                string sql = @"
                    SELECT stroke_data 
                    FROM DrawHistory 
                    WHERE room_id = (SELECT id FROM Rooms WHERE room_code = @code) 
                    ORDER BY timestamp ASC";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("code", roomCode);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    // Lấy chuỗi JSON của nét vẽ nhét vào danh sách
                    history.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB Error] GetRoomHistory: {ex.Message}");
            }
            return history;
        }
    }
}