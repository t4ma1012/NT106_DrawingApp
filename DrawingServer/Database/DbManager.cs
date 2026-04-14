using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace DrawingServer.Database
{
    public static class DbManager
    {
        private const string ConnectionString = "Host=localhost;Username=postgres;Password=123456;Database=drawingapp";

        private static readonly ConcurrentDictionary<string, string> InMemoryUsers = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, int> InMemoryUserIds = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> InMemoryRooms = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, List<string>> InMemoryRoomHistory = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, List<(string Filename, string ImageData, string CreatedBy, DateTime CreatedAt)>> InMemoryGallery = new ConcurrentDictionary<string, List<(string, string, string, DateTime)>>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, List<(string Username, string Message, DateTime SentAt)>> InMemoryChat = new ConcurrentDictionary<string, List<(string, string, DateTime)>>(StringComparer.OrdinalIgnoreCase);

        private static int _nextInMemoryUserId = 1;

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

        public static async Task<(bool IsSuccess, string Message)> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return (false, "Tên đăng nhập hoặc mật khẩu không hợp lệ");

            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                string hash = ComputeSha256Hash(password);
                using var cmdFind = new NpgsqlCommand("SELECT password_hash FROM Users WHERE username = @u", conn);
                cmdFind.Parameters.AddWithValue("u", username);
                var dbHash = await cmdFind.ExecuteScalarAsync() as string;

                if (!string.IsNullOrEmpty(dbHash))
                    return dbHash == hash ? (true, "Đăng nhập thành công") : (false, "Sai tài khoản hoặc mật khẩu");

                using var cmdInsert = new NpgsqlCommand("INSERT INTO Users (username, password_hash) VALUES (@u, @p)", conn);
                cmdInsert.Parameters.AddWithValue("u", username);
                cmdInsert.Parameters.AddWithValue("p", hash);
                await cmdInsert.ExecuteNonQueryAsync();
                return (true, "Đăng nhập thành công (tạo tài khoản mới)");
            }
            catch
            {
                string hash = ComputeSha256Hash(password);
                if (InMemoryUsers.TryGetValue(username, out var existingHash))
                    return existingHash == hash ? (true, "Đăng nhập thành công (local)") : (false, "Sai tài khoản hoặc mật khẩu");

                InMemoryUsers[username] = hash;
                InMemoryUserIds.TryAdd(username, Interlocked.Increment(ref _nextInMemoryUserId));
                return (true, "Đăng nhập thành công (local auto-register)");
            }
        }

        public static async Task<string> CreateRoomAsync(string username, int width, int height)
        {
            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                string roomCode = GenerateRoomCode();
                using var cmd = new NpgsqlCommand("INSERT INTO Rooms (room_code, created_by, canvas_width, canvas_height) VALUES (@code, @user, @w, @h) RETURNING room_code", conn);
                cmd.Parameters.AddWithValue("code", roomCode);
                cmd.Parameters.AddWithValue("user", username ?? "guest");
                cmd.Parameters.AddWithValue("w", width);
                cmd.Parameters.AddWithValue("h", height);
                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString() ?? roomCode;
            }
            catch
            {
                string roomCode = GenerateRoomCode();
                InMemoryRooms[roomCode] = 0;
                InMemoryRoomHistory.TryAdd(roomCode, new List<string>());
                return roomCode;
            }
        }

        public static async Task<bool> CheckRoomExistsAsync(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
                return false;

            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM Rooms WHERE room_code = @code", conn);
                cmd.Parameters.AddWithValue("code", roomCode);
                long count = (long)await cmd.ExecuteScalarAsync();
                return count > 0;
            }
            catch
            {
                return InMemoryRooms.ContainsKey(roomCode);
            }
        }

        public static async Task<bool> SaveStrokeAsync(string roomCode, string actionId, string strokeDataJson, string username)
        {
            if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(strokeDataJson))
                return false;

            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();
                string sql = @"INSERT INTO DrawHistory (room_id, action_id, stroke_data, username)
                               VALUES ((SELECT id FROM Rooms WHERE room_code = @code), @actionId, @strokeData::jsonb, @user)";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("code", roomCode);
                cmd.Parameters.AddWithValue("actionId", Guid.TryParse(actionId, out Guid guid) ? guid : Guid.NewGuid());
                cmd.Parameters.AddWithValue("strokeData", strokeDataJson);
                cmd.Parameters.AddWithValue("user", username ?? "unknown");
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch
            {
                InMemoryRooms[roomCode] = 0;
                var history = InMemoryRoomHistory.GetOrAdd(roomCode, _ => new List<string>());
                lock (history)
                {
                    history.Add(strokeDataJson);
                }
                return true;
            }
        }

        public static async Task<List<string>> GetRoomHistoryAsync(string roomCode)
        {
            var history = new List<string>();
            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();
                string sql = @"SELECT stroke_data FROM DrawHistory
                               WHERE room_id = (SELECT id FROM Rooms WHERE room_code = @code)
                               ORDER BY timestamp ASC";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("code", roomCode);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    history.Add(reader.GetString(0));
                }
            }
            catch
            {
                if (InMemoryRoomHistory.TryGetValue(roomCode, out var roomHistory))
                {
                    lock (roomHistory)
                    {
                        return new List<string>(roomHistory);
                    }
                }
            }
            return history;
        }

        public static async Task<int> GetUserIdAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return 0;

            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT id FROM Users WHERE username = @u", conn);
                cmd.Parameters.AddWithValue("u", username);
                var result = await cmd.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch
            {
                return InMemoryUserIds.GetOrAdd(username, _ => Interlocked.Increment(ref _nextInMemoryUserId));
            }
        }

        public static async Task<bool> SaveGalleryItemAsync(string roomCode, string filename, string imageData, string createdBy)
        {
            if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(imageData))
                return false;

            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmdInsert = new NpgsqlCommand(@"
                    INSERT INTO Gallery (room_id, filename, image_data, created_by, created_at)
                    VALUES ((SELECT id FROM Rooms WHERE room_code = @code), @file, @img, @user, NOW())", conn);
                cmdInsert.Parameters.AddWithValue("code", roomCode);
                cmdInsert.Parameters.AddWithValue("file", filename ?? "Untitled");
                cmdInsert.Parameters.AddWithValue("img", imageData);
                cmdInsert.Parameters.AddWithValue("user", createdBy ?? "unknown");
                await cmdInsert.ExecuteNonQueryAsync();
                return true;
            }
            catch
            {
                var gallery = InMemoryGallery.GetOrAdd(roomCode, _ => new List<(string, string, string, DateTime)>());
                lock (gallery)
                {
                    gallery.Add((filename ?? "Untitled", imageData, createdBy ?? "unknown", DateTime.Now));
                }
                return true;
            }
        }

        public static async Task<List<(string Filename, string ImageData, string CreatedBy, DateTime CreatedAt)>> GetGalleryItemsAsync(string roomCode)
        {
            var items = new List<(string, string, string, DateTime)>();
            if (string.IsNullOrWhiteSpace(roomCode))
                return items;

            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(@"
                    SELECT filename, image_data, created_by, created_at
                    FROM Gallery
                    WHERE room_id = (SELECT id FROM Rooms WHERE room_code = @code)
                    ORDER BY created_at DESC", conn);
                cmd.Parameters.AddWithValue("code", roomCode);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3)));
                }
                return items;
            }
            catch
            {
                if (InMemoryGallery.TryGetValue(roomCode, out var gallery))
                {
                    lock (gallery)
                    {
                        return new List<(string, string, string, DateTime)>(gallery);
                    }
                }
                return items;
            }
        }

        public static async Task<bool> SaveChatMessageAsync(string roomCode, string username, string message)
        {
            if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(message))
                return false;

            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmdInsert = new NpgsqlCommand(@"
                    INSERT INTO ChatHistory (room_id, username, message, sent_at)
                    VALUES ((SELECT id FROM Rooms WHERE room_code = @code), @user, @msg, NOW())", conn);
                cmdInsert.Parameters.AddWithValue("code", roomCode);
                cmdInsert.Parameters.AddWithValue("user", username ?? "unknown");
                cmdInsert.Parameters.AddWithValue("msg", message);
                await cmdInsert.ExecuteNonQueryAsync();
                return true;
            }
            catch
            {
                var messages = InMemoryChat.GetOrAdd(roomCode, _ => new List<(string, string, DateTime)>());
                lock (messages)
                {
                    messages.Add((username ?? "unknown", message, DateTime.Now));
                }
                return true;
            }
        }

        public static async Task<List<(string Username, string Message, DateTime SentAt)>> GetChatMessagesAsync(string roomCode, int limit = 100)
        {
            var messages = new List<(string, string, DateTime)>();
            if (string.IsNullOrWhiteSpace(roomCode))
                return messages;

            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(@"
                    SELECT username, message, sent_at
                    FROM ChatHistory
                    WHERE room_id = (SELECT id FROM Rooms WHERE room_code = @code)
                    ORDER BY sent_at DESC
                    LIMIT @limit", conn);
                cmd.Parameters.AddWithValue("code", roomCode);
                cmd.Parameters.AddWithValue("limit", limit);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    messages.Add((reader.GetString(0), reader.GetString(1), reader.GetDateTime(2)));
                }
                return messages;
            }
            catch
            {
                if (InMemoryChat.TryGetValue(roomCode, out var roomMessages))
                {
                    lock (roomMessages)
                    {
                        return roomMessages.Count <= limit
                            ? new List<(string, string, DateTime)>(roomMessages)
                            : new List<(string, string, DateTime)>(roomMessages.GetRange(roomMessages.Count - limit, limit));
                    }
                }
                return messages;
            }
        }

        private static string GenerateRoomCode()
        {
            var rnd = new Random();
            string roomCode;
            do
            {
                roomCode = rnd.Next(100000, 999999).ToString();
            }
            while (InMemoryRooms.ContainsKey(roomCode));

            return roomCode;
        }
    }
}