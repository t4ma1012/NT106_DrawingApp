// ============================================================
// DrawingServer/Network/SecureUdpServer.cs — FIX v3
// Thêm log chi tiết + fix broadcast FLOOD_FILL, SPRAY, TEXT
// ============================================================
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using SharedLib.Packets;
using SharedLib.Security;
using SharedLib.Logging;
using Newtonsoft.Json.Linq;
using DrawingServer.Services;

namespace DrawingServer.Network
{
    public class SecureUdpServer
    {
        private UdpClient _udpListener = null!;

        public async Task StartAsync(int udpPort = 8889)
        {
            _udpListener = new UdpClient(udpPort);
            Logger.Info("UDP", $"Secure UDP Server dang chay tren port {udpPort} (AES-256)...");

            while (true)
            {
                try
                {
                    UdpReceiveResult result = await _udpListener.ReceiveAsync();
                    _ = Task.Run(() => HandlePacketAsync(result));
                }
                catch (Exception ex)
                {
                    Logger.Warning("UDP", $"Lỗi vòng nhận UDP: {ex.Message}");
                }
            }
        }

        private async Task HandlePacketAsync(UdpReceiveResult result)
        {
            try
            {
                // Bước 1: Giải mã AES-256
                byte[] decryptedBytes = AesHelper.Decrypt(result.Buffer);
                Packet packet = Packet.Deserialize(decryptedBytes);
                bool isPointerPacket = packet.Cmd == CommandType.CURSOR || packet.Cmd == CommandType.LASER;

                string jsonPayload = PacketHelper.GetRawJson(packet);
                JObject data = JObject.Parse(jsonPayload);
                string username = data["Username"]?.ToString() ?? data["ActiveUser"]?.ToString() ?? "";
                string roomCodeFromPayload = data["RoomCode"]?.ToString() ?? "";

                // Log mỗi gói nhận được (giúp debug)
                if (!isPointerPacket)
                    Logger.Info("UDP", $"Nhận [{packet.Cmd}] từ {result.RemoteEndPoint} | Username='{username}'");

                // ✅ DEBUG: Log tất cả command types để xác nhận DRAW có đến không
                if (!isPointerPacket)
                    Logger.Info("UDP", $"[NON-CURSOR] Cmd={packet.Cmd} ({(int)packet.Cmd}) từ '{username}'");

                if (string.IsNullOrEmpty(username))
                {
                    Logger.Warning("UDP", "Gói không có Username, bỏ qua.");
                    return;
                }

                // Bước 2: Tìm session TCP theo Username
                ClientSession senderSession = null!;
                foreach (var kv in SecureTcpServer.Clients)
                {
                    bool sameUser = string.Equals(kv.Value.Username, username, StringComparison.OrdinalIgnoreCase);
                    bool sameRoom = string.IsNullOrWhiteSpace(roomCodeFromPayload)
                        || string.IsNullOrWhiteSpace(kv.Value.RoomCode)
                        || string.Equals(kv.Value.RoomCode, roomCodeFromPayload, StringComparison.OrdinalIgnoreCase);

                    if (sameUser && sameRoom)
                    {
                        senderSession = kv.Value;
                        break;
                    }
                }

                if (senderSession == null)
                {
                    Logger.Warning("UDP", $"Không tìm thấy TCP session cho username '{username}'. Danh sách clients hiện tại:");
                    foreach (var kv in SecureTcpServer.Clients)
                        Logger.Warning("UDP", $"  -> key={kv.Key}, username='{kv.Value.Username}', room='{kv.Value.RoomCode}'");
                    return;
                }

                // Bước 3: Lưu UdpEndPoint lần đầu
                if (senderSession.UdpEndPoint == null || !senderSession.UdpEndPoint.Equals(result.RemoteEndPoint))
                {
                    senderSession.UdpEndPoint = result.RemoteEndPoint;
                    Logger.Info("UDP", $"[+] Đăng ký UDP endpoint cho '{username}': {result.RemoteEndPoint}");
                }

                // Bước 4: Lấy roomCode từ TCP session
                string roomCode = !string.IsNullOrWhiteSpace(senderSession.RoomCode)
                    ? senderSession.RoomCode
                    : roomCodeFromPayload;
                if (string.IsNullOrEmpty(roomCode))
                {
                    Logger.Warning("UDP", $"User '{username}' chưa vào phòng nào (RoomCode rỗng).");
                    return;
                }

                if (!isPointerPacket)
                    Logger.Info("UDP", $"Broadcast [{packet.Cmd}] từ '{username}' trong phòng '{roomCode}'");

                if (packet.Cmd == CommandType.UDP_PING)
                {
                    Logger.Info("UDP", $"[UDP_PING] Registered endpoint for '{username}' in room '{roomCode}'.");
                    return;
                }

                if (packet.Cmd == CommandType.SET_TURNBASED || packet.Cmd == CommandType.TURN_CHANGE)
                {
                    var roomState = RoomService.GetRoomState(roomCode);
                    if (roomState != null)
                    {
                        if (packet.Cmd == CommandType.SET_TURNBASED)
                        {
                            bool isEnabled = data["IsEnabled"]?.ToObject<bool>() ?? false;
                            string activeUser = isEnabled ? (data["ActiveUser"]?.ToString() ?? username) : "";
                            roomState.IsTurnBasedEnabled = isEnabled;
                            roomState.ActiveDrawingUser = activeUser;
                            data["RoomCode"] = roomCode;
                            data["Username"] = username;
                            data["ActiveUser"] = activeUser;
                            packet.Payload = System.Text.Encoding.UTF8.GetBytes(data.ToString());
                            Logger.Info("UDP", $"[TURN_BASED] {(isEnabled ? "ON" : "OFF")} active='{activeUser}' phòng {roomCode}");
                        }
                        else if (RoomService.TryAdvanceTurn(roomCode, username, out string nextActiveUser, out string message))
                        {
                            roomState.IsTurnBasedEnabled = true;
                            roomState.ActiveDrawingUser = nextActiveUser;
                            data["RoomCode"] = roomCode;
                            data["Username"] = username;
                            data["IsEnabled"] = true;
                            data["ActiveUser"] = nextActiveUser;
                            packet.Payload = System.Text.Encoding.UTF8.GetBytes(data.ToString());
                            Logger.Info("UDP", $"[TURN_CHANGE] '{username}' chuyển lượt sang '{nextActiveUser}' phòng {roomCode}");
                        }
                        else
                        {
                            Logger.Warning("UDP", $"[TURN_CHANGE] Bỏ qua từ '{username}': {message}");
                            return;
                        }
                    }
                }

                if (IsDrawingCommand(packet.Cmd))
                {
                    var roomState = RoomService.GetRoomState(roomCode);
                    if (roomState != null &&
                        roomState.IsTurnBasedEnabled &&
                        !string.Equals(roomState.ActiveDrawingUser, username, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Info("UDP", $"[TURN_BASED] Chặn [{packet.Cmd}] từ '{username}', lượt hiện tại='{roomState.ActiveDrawingUser}'");
                        return;
                    }
                }

                // Bước 5: Xử lý theo loại lệnh
                // ✅ FIX: Lưu DB cho tất cả lệnh vẽ (FLOOD_FILL, TEXT, SPRAY)
                // để khi client reconnect, SYNC_BOARD trả về đầy đủ lịch sử
                if (packet.Cmd == CommandType.FLOOD_FILL
                 || packet.Cmd == CommandType.TEXT
                 || packet.Cmd == CommandType.SPRAY)
                {
                    string rawActionId = data["ActionID"]?.ToString() ?? "";
                    string actionId = Guid.TryParse(rawActionId, out var parsedGuid) ? parsedGuid.ToString() : Guid.NewGuid().ToString();
                    _ = Database.DbManager.SaveStrokeAsync(roomCode, actionId, jsonPayload, username);
                    Logger.Info("UDP", $"[SAVE] Lưu stroke [{packet.Cmd}] ActionID={actionId} phòng {roomCode}");
                }

                if (packet.Cmd == CommandType.DRAW)
                {
                    string rawActionId2 = data["ActionID"]?.ToString() ?? "";
                    string actionId = Guid.TryParse(rawActionId2, out var parsedGuid2) ? parsedGuid2.ToString() : Guid.NewGuid().ToString();
                    _ = Database.DbManager.SaveStrokeAsync(roomCode, actionId, jsonPayload, username);
                }
                else if (packet.Cmd == CommandType.PIXEL_ART_DRAW)
                {
                    // Lưu ô pixel vào DB (async, không block broadcast)
                    int row       = data["Row"]?.ToObject<int>()      ?? -1;
                    int col       = data["Col"]?.ToObject<int>()      ?? -1;
                    int colorArgb = data["ColorARGB"]?.ToObject<int>() ?? 0;

                    if (row >= 0 && col >= 0)
                        _ = Database.DbManager.SavePixelCellAsync(roomCode, row, col, colorArgb, username);

                    Logger.Info("UDP", $"[PIXEL_ART] '{username}' tô ô ({row},{col}) màu {colorArgb:X8}");
                }

                // Bước 6: Mã hóa và broadcast
                byte[] encryptedResponse = AesHelper.Encrypt(packet.Serialize());
                int sentCount = await BroadcastUdpAsync(encryptedResponse, roomCode, result.RemoteEndPoint, packet.Cmd);
                int tcpFallbackCount = 0;
                if (IsDrawingCommand(packet.Cmd) || packet.Cmd == CommandType.CHAT || packet.Cmd == CommandType.CURSOR || packet.Cmd == CommandType.LASER)
                {
                    tcpFallbackCount = await SecureTcpServer.BroadcastPacketToRoomWithoutUdpEndpointStaticAsync(
                        roomCode,
                        packet,
                        username);
                }
                if (IsCrossServerSyncCommand(packet.Cmd))
                    _ = CrossServerSyncService.PublishEventAsync(roomCode, packet, username);
                if (!isPointerPacket)
                {
                    Logger.Info("UDP", $"TCP fallback used for {tcpFallbackCount} client(s).");
                    Logger.Info("UDP", $"Đã broadcast tới {sentCount} client khác.");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("UDP", $"Lỗi xử lý packet: {ex.Message}");
            }
        }

        private async Task<int> BroadcastUdpAsync(byte[] encryptedData, string roomCode,
            IPEndPoint senderEndPoint, CommandType cmd)
        {
            // Sender already applies local state. Skipping it keeps high-frequency cursor/laser packets light.
            bool skipSender = true;
            bool isPointerPacket = cmd == CommandType.CURSOR || cmd == CommandType.LASER;
            int count = 0;

            foreach (var kv in SecureTcpServer.Clients)
            {
                ClientSession client = kv.Value;

                if (client.RoomCode != roomCode) continue;
                if (client.UdpEndPoint == null)
                {
                    if (!isPointerPacket)
                        Logger.Warning("UDP", $"Client '{client.Username}' chưa có UdpEndPoint, bỏ qua broadcast.");
                    continue;
                }
                if (skipSender && client.UdpEndPoint.Equals(senderEndPoint)) continue;

                try
                {
                    await _udpListener.SendAsync(encryptedData, encryptedData.Length, client.UdpEndPoint);
                    count++;
                }
                catch (Exception ex)
                {
                    Logger.Warning("UDP", $"Lỗi gửi UDP tới '{client.Username}': {ex.Message}");
                }
            }

            return count;
        }

        private static bool IsDrawingCommand(CommandType cmd)
        {
            return cmd == CommandType.DRAW
                || cmd == CommandType.FLOOD_FILL
                || cmd == CommandType.TEXT
                || cmd == CommandType.SPRAY
                || cmd == CommandType.SET_BACKGROUND
                || cmd == CommandType.STICKER
                || cmd == CommandType.IMPORT_IMAGE
                || cmd == CommandType.PIXEL_ART_DRAW;
        }

        private static bool IsCrossServerSyncCommand(CommandType cmd)
        {
            return cmd == CommandType.DRAW
                || cmd == CommandType.FLOOD_FILL
                || cmd == CommandType.TEXT
                || cmd == CommandType.SPRAY
                || cmd == CommandType.SET_BACKGROUND
                || cmd == CommandType.STICKER
                || cmd == CommandType.IMPORT_IMAGE
                || cmd == CommandType.PIXEL_ART_DRAW
                || cmd == CommandType.CHAT
                || cmd == CommandType.SET_TURNBASED
                || cmd == CommandType.TURN_CHANGE;
        }
    }
}
