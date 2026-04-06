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

                    // 1. GIẢI MÃ BẰNG AES-256 (Chuẩn của người B)
                    byte[] decryptedBytes = AesHelper.Decrypt(result.Buffer);
                    Packet packet = Packet.Deserialize(decryptedBytes);

                    if (packet.Cmd == CommandType.DRAW)
                    {
                        string jsonPayload = PacketHelper.GetRawJson(packet);
                        JObject drawData = JObject.Parse(jsonPayload);
                        string roomCode = drawData["RoomCode"]?.ToString() ?? "";

                        // 2. LƯU LỊCH SỬ VÀO DATABASE
                        if (!string.IsNullOrEmpty(roomCode))
                        {
                            string actionId = drawData["ActionID"]?.ToString() ?? Guid.NewGuid().ToString();
                            string username = drawData["Username"]?.ToString() ?? "unknown";
                            _ = Database.DbManager.SaveStrokeAsync(roomCode, actionId, jsonPayload, username);
                        }

                        // 3. MÃ HÓA LẠI VÀ BROADCAST CHO TẤT CẢ CLIENT TRONG PHÒNG
                        byte[] encryptedResponse = AesHelper.Encrypt(packet.Serialize());

                        foreach (var client in SecureTcpServer.Clients.Values)
                        {
                            if (client.RoomCode == roomCode && client.UdpEndPoint != null)
                            {
                                if (!client.UdpEndPoint.Equals(result.RemoteEndPoint))
                                {
                                    await _udpListener.SendAsync(encryptedResponse, encryptedResponse.Length, client.UdpEndPoint);
                                }
                            }
                        }
                    }
                }
                catch (Exception) { /* Bỏ qua gói tin lỗi/bị hack */ }
            }
        }
    }
}