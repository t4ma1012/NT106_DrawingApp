// ============================================================
// DrawingServer/Network/SecureUdpServer.cs — FIX v3
// Thêm log chi tiết + fix broadcast FLOOD_FILL, SPRAY, TEXT
// ============================================================
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using SharedLib.Packets;
using SharedLib.Security;
using SharedLib.Logging;
using Newtonsoft.Json.Linq;

namespace DrawingServer.Network
{
    public class SecureUdpServer
    {
        private UdpClient _udpListener = null!;

        public async Task StartAsync()
        {
            _udpListener = new UdpClient(8889);
            Logger.Info("UDP", "Secure UDP Server đang chạy trên port 8889 (AES-256)...");

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

                string jsonPayload = PacketHelper.GetRawJson(packet);
                JObject data = JObject.Parse(jsonPayload);
                string username = data["Username"]?.ToString() ?? "";

                // Log mỗi gói nhận được (giúp debug)
                Logger.Info("UDP", $"Nhận [{packet.Cmd}] từ {result.RemoteEndPoint} | Username='{username}'");

                if (string.IsNullOrEmpty(username))
                {
                    Logger.Warning("UDP", "Gói không có Username, bỏ qua.");
                    return;
                }

                // Bước 2: Tìm session TCP theo Username
                ClientSession senderSession = null;
                foreach (var kv in SecureTcpServer.Clients)
                {
                    if (string.Equals(kv.Value.Username, username, StringComparison.OrdinalIgnoreCase))
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
                if (senderSession.UdpEndPoint == null)
                {
                    senderSession.UdpEndPoint = result.RemoteEndPoint;
                    Logger.Info("UDP", $"[+] Đăng ký UDP endpoint cho '{username}': {result.RemoteEndPoint}");
                }

                // Bước 4: Lấy roomCode từ TCP session
                string roomCode = senderSession.RoomCode;
                if (string.IsNullOrEmpty(roomCode))
                {
                    Logger.Warning("UDP", $"User '{username}' chưa vào phòng nào (RoomCode rỗng).");
                    return;
                }

                Logger.Info("UDP", $"Broadcast [{packet.Cmd}] từ '{username}' trong phòng '{roomCode}'");

                // Bước 5: Lưu DB nếu là DRAW
                if (packet.Cmd == CommandType.DRAW)
                {
                    string actionId = data["ActionID"]?.ToString() ?? Guid.NewGuid().ToString();
                    _ = Database.DbManager.SaveStrokeAsync(roomCode, actionId, jsonPayload, username);
                }

                // Bước 6: Mã hóa và broadcast
                byte[] encryptedResponse = AesHelper.Encrypt(packet.Serialize());
                int sentCount = await BroadcastUdpAsync(encryptedResponse, roomCode, result.RemoteEndPoint, packet.Cmd);
                Logger.Info("UDP", $"Đã broadcast tới {sentCount} client khác.");
            }
            catch (Exception ex)
            {
                Logger.Warning("UDP", $"Lỗi xử lý packet: {ex.Message}");
            }
        }

        private async Task<int> BroadcastUdpAsync(byte[] encryptedData, string roomCode,
            IPEndPoint senderEndPoint, CommandType cmd)
        {
            // CURSOR và LASER gửi cho tất cả kể cả người gửi
            // Các lệnh vẽ bỏ qua người gửi (tránh vẽ đè lên chính mình)
            bool skipSender = (cmd != CommandType.CURSOR && cmd != CommandType.LASER);
            int count = 0;

            foreach (var kv in SecureTcpServer.Clients)
            {
                ClientSession client = kv.Value;

                if (client.RoomCode != roomCode) continue;
                if (client.UdpEndPoint == null)
                {
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
    }
}
