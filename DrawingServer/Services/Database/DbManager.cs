using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using SharedLib.Logging; // Thư viện kết nối PostgreSQL

namespace DrawingServer.Database // Đảm bảo đúng Namespace này để các file khác nhận diện được
{
    public static class DbManager
    {
        // Nhớ kiểm tra lại mật khẩu Database của bạn (đang để mặc định là 123456)
        private static readonly string connString = "Host=your-db-host;Port=5432;Database=drawingapp;Username=your_user;Password=your_password;";

        private static string ComputeSha256Hash(string rawData)
        {
            if (string.IsNullOrEmpty(rawData)) return "";
            using (System.Security.Cryptography.SHA256 sha256Hash = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                    builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }

        /// <summary>Xử lý Đăng nhập / Đăng ký tự động</summary>
        public static async Task<(bool IsSuccess, string Message)> LoginAsync(string username, string password)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                string hashedPass = ComputeSha256Hash(password);

                // Kiểm tra user tồn tại chưa
                using var cmd = new NpgsqlCommand("SELECT password_hash FROM Users WHERE username = @u", conn);
                cmd.Parameters.AddWithValue("u", username);
                var dbPass = await cmd.ExecuteScalarAsync() as string;

                if (dbPass != null)
                {
                    if (dbPass == hashedPass || dbPass == password) return (true, "Đăng nhập thành công!");
                    return (false, "Sai mật khẩu!");
                }
                else
                {
                    // Nếu chưa có thì tạo mới (Auto-Register)
                    using var cmdInsert = new NpgsqlCommand("INSERT INTO Users (username, password_hash) VALUES (@u, @p)", conn);
                    cmdInsert.Parameters.AddWithValue("u", username);
                    cmdInsert.Parameters.AddWithValue("p", hashedPass);
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

                // Lấy user_id thay vì cố gắng nhét chữ
                using var cmdUserId = new NpgsqlCommand("SELECT id FROM Users WHERE username = @u", conn);
                cmdUserId.Parameters.AddWithValue("u", username);
                var userIdObj = await cmdUserId.ExecuteScalarAsync();

                if (userIdObj == null) 
                    return null; // Không tìm thấy user

                int userId = Convert.ToInt32(userIdObj);

                string roomCode = new Random().Next(100000, 999999).ToString();

                // Dùng owner_id như đúng thiết kế bảng Rooms của bạn
                using var cmdRoom = new NpgsqlCommand("INSERT INTO rooms (room_code, owner_id, canvas_width, canvas_height) VALUES (@c, @oid, @w, @h)", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                cmdRoom.Parameters.AddWithValue("oid", userId);
                cmdRoom.Parameters.AddWithValue("w", width);
                cmdRoom.Parameters.AddWithValue("h", height);
                await cmdRoom.ExecuteNonQueryAsync();

                return roomCode;
            }
            catch (Exception ex)
            {
                SharedLib.Logging.Logger.Error("DB", "Lỗi tạo phòng: " + ex.Message);
                return null;
            }
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
                var roomIdObj = await cmdRoom.ExecuteScalarAsync();
                if (roomIdObj == null || roomIdObj == DBNull.Value)
                {
                    SharedLib.Logging.Logger.Error("DB", $"[SAVE ERROR] Không tìm thấy phòng room_code='{roomCode}'");
                    return;
                }
                int roomId = Convert.ToInt32(roomIdObj);

                using var cmdInsert = new NpgsqlCommand("INSERT INTO DrawHistory (room_id, action_id, stroke_data, username) VALUES (@r, @a::uuid, @s::jsonb, @u)", conn);
                cmdInsert.Parameters.AddWithValue("r", roomId);
                cmdInsert.Parameters.AddWithValue("a", actionId);
                cmdInsert.Parameters.AddWithValue("s", strokeData);
                cmdInsert.Parameters.AddWithValue("u", username ?? "");
                await cmdInsert.ExecuteNonQueryAsync();
                SharedLib.Logging.Logger.Info("DB", $"[SAVE] stroke room={roomCode} action={actionId}");
            }
            catch (Exception ex)
            {
                SharedLib.Logging.Logger.Error("DB", $"[SAVE ERROR] room={roomCode} action={actionId} err={ex.Message}");
            }
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
                var roomIdObj = await cmdRoom.ExecuteScalarAsync();
                if (roomIdObj == null || roomIdObj == DBNull.Value)
                {
                    SharedLib.Logging.Logger.Error("DB", $"[HISTORY ERROR] Không tìm thấy phòng room_code='{roomCode}'");
                    return history;
                }
                int roomId = Convert.ToInt32(roomIdObj);

                using var cmd = new NpgsqlCommand("SELECT stroke_data FROM DrawHistory WHERE room_id = @r ORDER BY id ASC", conn);
                cmd.Parameters.AddWithValue("r", roomId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    history.Add(reader.GetString(0));

                SharedLib.Logging.Logger.Info("DB", $"[HISTORY] room={roomCode} → {history.Count} strokes");
            }
            catch (Exception ex)
            {
                SharedLib.Logging.Logger.Error("DB", $"[HISTORY ERROR] room={roomCode} err={ex.Message}");
            }
            return history;
        }

        /// <summary>Xóa toàn bộ lịch sử vẽ của phòng (khi CLEAR_ALL).</summary>
        public static async Task ClearRoomHistoryAsync(string roomCode)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomIdObj = await cmdRoom.ExecuteScalarAsync();
                if (roomIdObj == null) return;
                int roomId = Convert.ToInt32(roomIdObj);

                using var cmd = new NpgsqlCommand("DELETE FROM DrawHistory WHERE room_id = @r", conn);
                cmd.Parameters.AddWithValue("r", roomId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Logger.Warning("DB", $"ClearRoomHistoryAsync lỗi: {ex.Message}");
            }
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
        /// <summary>
        /// Lưu ảnh vào Gallery. Trả về (id, publicToken) để tạo public link ngay.
        /// public_token được PostgreSQL tự sinh bằng gen_random_uuid() trong database_setup.sql.
        /// </summary>
        private static async Task EnsureGalleryTableAsync(NpgsqlConnection conn)
        {
            using var cmd = new NpgsqlCommand(
                @"CREATE TABLE IF NOT EXISTS Gallery (
                    id SERIAL PRIMARY KEY,
                    room_id INT REFERENCES Rooms(id) ON DELETE CASCADE,
                    filename VARCHAR(255) NOT NULL,
                    image_data TEXT NOT NULL,
                    created_by VARCHAR(50),
                    created_at TIMESTAMPTZ DEFAULT NOW(),
                    public_token VARCHAR(100) UNIQUE
                );
                CREATE INDEX IF NOT EXISTS idx_gallery_room_id ON Gallery(room_id);
                CREATE INDEX IF NOT EXISTS idx_gallery_public_token ON Gallery(public_token);",
                conn);
            await cmd.ExecuteNonQueryAsync();
        }

        public static async Task<(bool IsSuccess, int Id, string PublicToken)> SaveGalleryItemAsync(
            string roomCode, string filename, string imageData, string createdBy)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();
                await EnsureGalleryTableAsync(conn);

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomIdObj = await cmdRoom.ExecuteScalarAsync();
                if (roomIdObj == null || roomIdObj == DBNull.Value) return (false, 0, "");
                int roomId = Convert.ToInt32(roomIdObj);
                string tokenValue = Guid.NewGuid().ToString("N");

                // RETURNING id, public_token để lấy luôn sau khi insert
                using var cmdInsert = new NpgsqlCommand(
                    @"INSERT INTO Gallery (room_id, filename, image_data, created_by, created_at, public_token)
                      VALUES (@r, @f, @i, @cb, NOW(), @t)
                      RETURNING id, public_token",
                    conn);
                cmdInsert.Parameters.AddWithValue("r",  roomId);
                cmdInsert.Parameters.AddWithValue("f",  filename ?? "Untitled");
                cmdInsert.Parameters.AddWithValue("i",  imageData ?? "");
                cmdInsert.Parameters.AddWithValue("cb", createdBy);
                cmdInsert.Parameters.AddWithValue("t",  tokenValue);

                using var reader = await cmdInsert.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int    id    = reader.GetInt32(0);
                    string token = reader.GetString(1);
                    return (true, id, token);
                }
                return (false, 0, "");
            }
            catch (Exception ex)
            {
                Logger.Warning("DB", $"SaveGalleryItemAsync lỗi: {ex.Message}");
                return (false, 0, "");
            }
        }

        /// <summary>
        /// Lấy danh sách gallery của phòng, bao gồm id và public_token.
        /// </summary>
        public static async Task<List<(int Id, string Filename, string ImageData, string CreatedBy, DateTime CreatedAt, string PublicToken)>>
            GetGalleryItemsAsync(string roomCode)
        {
            var items = new List<(int, string, string, string, DateTime, string)>();
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();
                await EnsureGalleryTableAsync(conn);

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomIdObj = await cmdRoom.ExecuteScalarAsync();
                if (roomIdObj == null || roomIdObj == DBNull.Value) return items;
                int roomId = Convert.ToInt32(roomIdObj);

                using var cmd = new NpgsqlCommand(
                    @"SELECT id, filename, image_data, created_by, created_at, public_token
                      FROM Gallery WHERE room_id = @r ORDER BY created_at DESC",
                    conn);
                cmd.Parameters.AddWithValue("r", roomId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetDateTime(4),
                        reader.IsDBNull(5) ? "" : reader.GetString(5)
                    ));
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("DB", $"GetGalleryItemsAsync lỗi: {ex.Message}");
            }
            return items;
        }

        /// <summary>
        /// Lấy 1 item gallery theo public_token (dùng cho public link — không cần đăng nhập).
        /// Trả về null nếu không tìm thấy.
        /// </summary>
        public static async Task<(int Id, string Filename, string ImageData, string CreatedBy, DateTime CreatedAt)?> 
            GetPublicGalleryItemAsync(string publicToken)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(
                    "SELECT id, filename, image_data, created_by, created_at FROM Gallery WHERE public_token = @t",
                    conn);
                cmd.Parameters.AddWithValue("t", publicToken);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                    return (reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                            reader.GetString(3), reader.GetDateTime(4));
            }
            catch (Exception ex)
            {
                Logger.Warning("DB", $"GetPublicGalleryItemAsync lỗi: {ex.Message}");
            }
            return null;
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


        /// <summary>Lưu kết quả AI vào bảng AiHistory</summary>
        public static async Task<bool> SaveAiResultAsync(string roomCode, string aiType, string prompt, string imageData, string username)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                // Lấy room_id
                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                if (roomId == null) return false;

                using var cmdInsert = new NpgsqlCommand(
                    @"INSERT INTO AiHistory (room_id, ai_type, prompt, image_data, created_by, created_at)
                      VALUES (@r, @t, @p, @i, @u, NOW())", conn);
                cmdInsert.Parameters.AddWithValue("r", roomId.Value);
                cmdInsert.Parameters.AddWithValue("t", aiType);
                cmdInsert.Parameters.AddWithValue("p", prompt ?? "");
                cmdInsert.Parameters.AddWithValue("i", imageData ?? "");
                cmdInsert.Parameters.AddWithValue("u", username);
                await cmdInsert.ExecuteNonQueryAsync();

                Logger.Info("DB", $"Đã lưu AI result [{aiType}] của {username} vào phòng {roomCode}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning("DB", $"Lỗi lưu AI result: {ex.Message}");
                return false;
            }
        }

        /// <summary>Lấy lịch sử AI của phòng</summary>
        public static async Task<List<(string AiType, string Prompt, string CreatedBy, DateTime CreatedAt)>> GetAiHistoryAsync(string roomCode, int limit = 50)
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
                    @"SELECT ai_type, prompt, created_by, created_at FROM AiHistory
                      WHERE room_id = @r ORDER BY created_at DESC LIMIT @l", conn);
                cmd.Parameters.AddWithValue("r", roomId.Value);
                cmd.Parameters.AddWithValue("l", limit);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    items.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3)));
            }
            catch (Exception ex) { Logger.Warning("DB", $"Lỗi lấy AI history: {ex.Message}"); }
            return items;
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

        // ── PIXEL ART ──────────────────────────────────────────────────────────

        /// <summary>
        /// Lưu 1 ô pixel vào DB (upsert: nếu đã tồn tại thì cập nhật màu).
        /// Bảng: PixelArtCells(room_id INT, row INT, col INT, color_argb INT, username VARCHAR, updated_at TIMESTAMPTZ)
        /// </summary>
        public static async Task SavePixelCellAsync(string roomCode, int row, int col, int colorArgb, string username)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                if (roomId == null) return;

                // INSERT ... ON CONFLICT (room_id, row, col) DO UPDATE
                const string sql = @"
                    INSERT INTO PixelArtCells (room_id, row, col, color_argb, username, updated_at)
                    VALUES (@r, @row, @col, @color, @user, NOW())
                    ON CONFLICT (room_id, row, col)
                    DO UPDATE SET color_argb = EXCLUDED.color_argb,
                                  username   = EXCLUDED.username,
                                  updated_at = EXCLUDED.updated_at";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("r",    roomId);
                cmd.Parameters.AddWithValue("row",  row);
                cmd.Parameters.AddWithValue("col",  col);
                cmd.Parameters.AddWithValue("color", colorArgb);
                cmd.Parameters.AddWithValue("user", username);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Logger.Warning("DB", $"SavePixelCellAsync lỗi: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy toàn bộ trạng thái pixel art của 1 phòng.
        /// Trả về list (row, col, colorArgb).
        /// </summary>
        public static async Task<List<(int Row, int Col, int ColorArgb)>> GetPixelBoardAsync(string roomCode)
        {
            var cells = new List<(int, int, int)>();
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                if (roomId == null) return cells;

                using var cmd = new NpgsqlCommand(
                    "SELECT row, col, color_argb FROM PixelArtCells WHERE room_id = @r",
                    conn);
                cmd.Parameters.AddWithValue("r", roomId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    cells.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)));
            }
            catch (Exception ex)
            {
                Logger.Warning("DB", $"GetPixelBoardAsync lỗi: {ex.Message}");
            }
            return cells;
        }

        /// <summary>Xóa toàn bộ pixel art của 1 phòng (khi cần reset).</summary>
        public static async Task ClearPixelBoardAsync(string roomCode)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                if (roomId == null) return;

                using var cmd = new NpgsqlCommand("DELETE FROM PixelArtCells WHERE room_id = @r", conn);
                cmd.Parameters.AddWithValue("r", roomId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Logger.Warning("DB", $"ClearPixelBoardAsync lỗi: {ex.Message}");
            }
        }

        // ── SNAPSHOT ───────────────────────────────────────────────────────────

        /// <summary>
        /// Lưu 1 snapshot (toàn bộ DrawHistory tại thời điểm này dưới dạng JSON gộp).
        /// Bảng: Snapshots(id, room_id, snapshot_data JSONB, thumbnail TEXT, taken_at TIMESTAMPTZ)
        /// </summary>
        public static async Task<int> SaveSnapshotAsync(string roomCode, string snapshotJson, string thumbnailBase64 = "")
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                if (roomId == null) return 0;

                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO Snapshots (room_id, snapshot_data, thumbnail, taken_at)
                      VALUES (@r, @d::jsonb, @t, NOW()) RETURNING id",
                    conn);
                cmd.Parameters.AddWithValue("r", roomId);
                cmd.Parameters.AddWithValue("d", snapshotJson);
                cmd.Parameters.AddWithValue("t", thumbnailBase64);

                var result = await cmd.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception ex)
            {
                Logger.Warning("DB", $"SaveSnapshotAsync lỗi: {ex.Message}");
                return 0;
            }
        }

        /// <summary>Lấy danh sách snapshot của phòng (không kèm data lớn, chỉ meta + thumbnail).</summary>
        public static async Task<List<(int Id, DateTime TakenAt, string Thumbnail)>> GetSnapshotListAsync(string roomCode)
        {
            var list = new List<(int, DateTime, string)>();
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                if (roomId == null) return list;

                using var cmd = new NpgsqlCommand(
                    "SELECT id, taken_at, thumbnail FROM Snapshots WHERE room_id = @r ORDER BY taken_at DESC",
                    conn);
                cmd.Parameters.AddWithValue("r", roomId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add((reader.GetInt32(0), reader.GetDateTime(1), reader.IsDBNull(2) ? "" : reader.GetString(2)));
            }
            catch (Exception ex)
            {
                Logger.Warning("DB", $"GetSnapshotListAsync lỗi: {ex.Message}");
            }
            return list;
        }

        /// <summary>Lấy toàn bộ stroke_data của 1 snapshot theo id.</summary>
        public static async Task<string> GetSnapshotDataAsync(int snapshotId)
        {
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("SELECT snapshot_data FROM Snapshots WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("id", snapshotId);
                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                Logger.Warning("DB", $"GetSnapshotDataAsync lỗi: {ex.Message}");
                return "";
            }
        }

        // ── TIMELINE ───────────────────────────────────────────────────────────

        /// <summary>
        /// Lấy tất cả stroke_data trong phòng tính đến thời điểm targetTimestamp (Unix ms).
        /// Dùng cho Time Travel — client kéo thanh timeline về quá khứ.
        /// </summary>
        public static async Task<List<string>> GetHistoryUntilAsync(string roomCode, long targetTimestampMs)
        {
            var history = new List<string>();
            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                using var cmdRoom = new NpgsqlCommand("SELECT id FROM Rooms WHERE room_code = @c", conn);
                cmdRoom.Parameters.AddWithValue("c", roomCode);
                var roomId = await cmdRoom.ExecuteScalarAsync() as int?;
                if (roomId == null) return history;

                using var cmd = new NpgsqlCommand(
                    @"SELECT stroke_data FROM DrawHistory
                      WHERE room_id = @r
                        AND created_at <= to_timestamp(@ts / 1000.0)
                      ORDER BY id ASC",
                    conn);
                cmd.Parameters.AddWithValue("r",  roomId);
                cmd.Parameters.AddWithValue("ts", targetTimestampMs);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    history.Add(reader.GetString(0));
            }
            catch (Exception ex)
            {
                Logger.Warning("DB", $"GetHistoryUntilAsync lỗi: {ex.Message}");
            }
            return history;
        }
    }
}
