using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SharedLib.Packets; // Sử dụng PacketDef của Người B

namespace DrawingServer
{
    public class UdpServer
    {
        // Khai báo = null! để fix warning CS8618
        private UdpClient _udpListener = null!;

        public async Task StartAsync()
        {
            _udpListener = new UdpClient(8889);
            Console.WriteLine("UDP Server dang chay tren port 8889 (Realtime Broadcast)...");

            while (true)
            {
                try
                {
                    UdpReceiveResult result = await _udpListener.ReceiveAsync();
                    byte[] receivedBytes = result.Buffer; // Biến ở đây tên là receivedBytes
                    IPEndPoint senderEndPoint = result.RemoteEndPoint;

                    // Mở gói tin UDP bằng PacketDef (đã sửa thành receivedBytes)
                    Packet packet = Packet.Deserialize(receivedBytes);

                    // Nếu là lệnh VẼ (DRAW)
                    if (packet.Cmd == CommandType.DRAW)
                    {
                        string jsonPayload = Encoding.UTF8.GetString(packet.Payload);

                        // Phân tích JSON để lấy mã phòng, người vẽ và ActionId
                        var drawData = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, JsonElement>>(jsonPayload);

                        string roomCode = drawData != null && drawData.ContainsKey("RoomCode") ? drawData["RoomCode"].GetString() ?? "" : "";
                        string actionId = drawData != null && drawData.ContainsKey("ActionId") ? drawData["ActionId"].GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                        string username = drawData != null && drawData.ContainsKey("Username") ? drawData["Username"].GetString() ?? "unknown" : "unknown";

                        // 1. LƯU NÉT VẼ VÀO DATABASE (Chạy ngầm không đợi để tránh lag)
                        if (!string.IsNullOrEmpty(roomCode))
                        {
                            _ = Database.DbManager.SaveStrokeAsync(roomCode, actionId, jsonPayload, username);
                        }

                        // 2. CHỈ GỬI CHO NHỮNG NGƯỜI TRONG CÙNG PHÒNG (Ngoại trừ người vừa vẽ)
                        foreach (var client in Server.Clients.Values)
                        {
                            // Kiểm tra xem Client này có đang ở đúng cái phòng đó không
                            if (client.RoomCode == roomCode && client.UdpEndPoint != null)
                            {
                                // Không gửi ngược lại nét vẽ cho chính người vừa vẽ
                                if (!client.UdpEndPoint.Equals(result.RemoteEndPoint))
                                {
                                    // Đã sửa thành receivedBytes
                                    await _udpListener.SendAsync(receivedBytes, receivedBytes.Length, client.UdpEndPoint);
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Bỏ qua im lặng các gói tin rác hoặc không parse được
                }
            }
        }
    }
}