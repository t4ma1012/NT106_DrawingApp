using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql; // Thư viện kết nối PostgreSQL

namespace DrawingServer.Database // Đảm bảo đúng Namespace này để các file khác nhận diện được
{
    public static class DbManager
    {
        // Nhớ kiểm tra lại mật khẩu Database của bạn (đang để mặc định là 123456)
        private static readonly string connString = "Host=127.0.0.1;Port=5432;Database=drawingapp;Username=postgres;Password=123456";

        /// <summary>Xử lý Đăng nhập / Đăng ký tự động</summary>
        public static async Task<(bool IsSuccess, string Message)> LoginAsync(string username, string password)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                // Kiểm tra user tồn tại chưa
                using var cmd = new NpgsqlCommand("SELECT password_hash FROM Users WHERE username = @u", conn);
                cmd.Parameters.AddWithValue("u", username);
                var dbPass = await cmd.ExecuteScalarAsync() as string;

                if (dbPass != null)
                {
                    if (dbPass == password) return (true, "Đăng nhập thành công!");
                    return (false, "Sai mật khẩu!");
                }
                else
                {
                    // Nếu chưa có thì tạo mới (Auto-Register)
                    using var cmdInsert = new NpgsqlCommand("INSERT INTO Users (username, password_hash) VALUES (@u, @p)", conn);
                    cmdInsert.Parameters.AddWithValue("u", username);
                    cmdInsert.Parameters.AddWithValue("p", password);
                    await cmdInsert.ExecuteNonQueryAsync();
                    return (true, "Tạo tài khoản thành công!");
                }
            }
            catch (Exception ex) { return (false, "Lỗi kết nối DB: " + ex.Message); }
        }

        /// <summary>Tạo phòng mới và trả về mã phòng ngẫu nhiên</summary>
        public static async Task<string> CreateRoomAsync(string username, int width, int height)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                // Lấy ID người tạo
                using var cmdUser = new NpgsqlCommand("SELECT id FROM Users WHERE username = @u", conn);
                cmdUser.Parameters.AddWithValue("u", username);
                var userId = Convert.ToInt32(await cmdUser.ExecuteScalarAsync());

                string roomCode = new Random().Next(100000, 999999).ToString();

                using var cmdRoom = new NpgsqlCommand("INSERT INTO Rooms (room_code, owner_id) VALUES (@c, @o)", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                cmdRoom.Parameters.AddWithValue("o", userId);
                await cmdRoom.ExecuteNonQueryAsync();

                return roomCode;
            }
            catch { return null; }
        }

        /// <summary>Kiểm tra phòng có tồn tại không</summary>
        public static async Task<bool> CheckRoomExistsAsync(string roomCode)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM Rooms WHERE room_code = @c", conn);
                cmd.Parameters.AddWithValue("c", roomCode);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            }
            catch { return false; }
        }

        /// <summary>Lưu nét vẽ vào lịch sử (Dùng cho UDP)</summary>
        public static async Task SaveStrokeAsync(string roomCode, string actionId, string strokeData, string username)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = Convert.ToInt32(await cmdRoom.ExecuteScalarAsync());

                using var cmdInsert = new NpgsqlCommand("INSERT INTO DrawHistory (room_id, action_id, stroke_data) VALUES (@r, @a, @s::jsonb)", conn);
                cmdInsert.Parameters.AddWithValue("r", roomId);
                cmdInsert.Parameters.AddWithValue("a", actionId);
                cmdInsert.Parameters.AddWithValue("s", strokeData);
                await cmdInsert.ExecuteNonQueryAsync();
            }
            catch { }
        }

        /// <summary>Lấy toàn bộ lịch sử vẽ khi User mới vào phòng (Sync Board)</summary>
        public static async Task<List<string>> GetRoomHistoryAsync(string roomCode)
        {
            var history = new List<string>();
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = Convert.ToInt32(await cmdRoom.ExecuteScalarAsync());

                using var cmd = new NpgsqlCommand("SELECT stroke_data FROM DrawHistory WHERE room_id = @r ORDER BY id ASC", conn);
                cmd.Parameters.AddWithValue("r", roomId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    history.Add(reader.GetString(0));
                }
            }
            catch { }
            return history;
        }

        /// <summary>Get User ID by username (for auth service)</summary>
        public static async Task<int> GetUserIdAsync(string username)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT id FROM Users WHERE username = @u", conn);
                cmd.Parameters.AddWithValue("u", username);
                var result = await cmd.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch { return 0; }
        }

        /// <summary>Save gallery item (drawing export)</summary>
        public static async Task<bool> SaveGalleryItemAsync(string roomCode, string filename, string imageData, string createdBy)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                
                if (roomId == null) return false;

                using var cmdInsert = new NpgsqlCommand(
                    "INSERT INTO Gallery (room_id, filename, image_data, created_by, created_at) VALUES (@r, @f, @i, @cb, NOW())",
                    conn);
                cmdInsert.Parameters.AddWithValue("r", roomId);
                cmdInsert.Parameters.AddWithValue("f", filename ?? "Untitled");
                cmdInsert.Parameters.AddWithValue("i", imageData);
                cmdInsert.Parameters.AddWithValue("cb", createdBy);
                
                await cmdInsert.ExecuteNonQueryAsync();
                return true;
            }
            catch { return false; }
        }

        /// <summary>Get gallery items for a room</summary>
        public static async Task<List<(string Filename, string ImageData, string CreatedBy, DateTime CreatedAt)>> GetGalleryItemsAsync(string roomCode)
        {
            var items = new List<(string, string, string, DateTime)>();
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                
                if (roomId == null) return items;

                using var cmd = new NpgsqlCommand(
                    "SELECT filename, image_data, created_by, created_at FROM Gallery WHERE room_id = @r ORDER BY created_at DESC",
                    conn);
                cmd.Parameters.AddWithValue("r", roomId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add((
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetDateTime(3)
                    ));
                }
            }
            catch { }
            return items;
        }

        /// <summary>Save chat message</summary>
        public static async Task<bool> SaveChatMessageAsync(string roomCode, string username, string message)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                
                if (roomId == null) return false;

                using var cmdInsert = new NpgsqlCommand(
                    "INSERT INTO ChatHistory (room_id, username, message, sent_at) VALUES (@r, @u, @m, NOW())",
                    conn);
                cmdInsert.Parameters.AddWithValue("r", roomId);
                cmdInsert.Parameters.AddWithValue("u", username);
                cmdInsert.Parameters.AddWithValue("m", message);
                
                await cmdInsert.ExecuteNonQueryAsync();
                return true;
            }
            catch { return false; }
        }

        /// <summary>Get chat messages for a room</summary>
        public static async Task<List<(string Username, string Message, DateTime SentAt)>> GetChatMessagesAsync(string roomCode, int limit = 100)
        {
            var messages = new List<(string, string, DateTime)>();
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                
                if (roomId == null) return messages;

                using var cmd = new NpgsqlCommand(
                    "SELECT username, message, sent_at FROM ChatHistory WHERE room_id = @r ORDER BY sent_at DESC LIMIT @l",
                    conn);
                cmd.Parameters.AddWithValue("r", roomId);
                cmd.Parameters.AddWithValue("l", limit);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    messages.Add((
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetDateTime(2)
                    ));
                }
            }
            catch { }
            return messages;
        }

        /// <summary>Save undo/redo action</summary>
        public static async Task<bool> SaveActionStackAsync(string roomCode, string actionJson, bool isUndo)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                
                if (roomId == null) return false;

                using var cmdInsert = new NpgsqlCommand(
                    "INSERT INTO ActionStack (room_id, action_data, is_undo, created_at) VALUES (@r, @d::jsonb, @u, NOW())",
                    conn);
                cmdInsert.Parameters.AddWithValue("r", roomId);
                cmdInsert.Parameters.AddWithValue("d", actionJson);
                cmdInsert.Parameters.AddWithValue("u", isUndo);
                
                await cmdInsert.ExecuteNonQueryAsync();
                return true;
            }
            catch { return false; }
        }

        /// <summary>Get all undo/redo actions for a room</summary>
        public static async Task<List<string>> GetActionStackAsync(string roomCode)
        {
            var actions = new List<string>();
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                
                if (roomId == null) return actions;

                using var cmd = new NpgsqlCommand(
                    "SELECT action_data FROM ActionStack WHERE room_id = @r ORDER BY created_at ASC",
                    conn);
                cmd.Parameters.AddWithValue("r", roomId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    actions.Add(reader.GetString(0));
                }
            }
            catch { }
            return actions;
        }
    }
}