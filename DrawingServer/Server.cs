using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SharedLib.Packets; // Sử dụng PacketDef của Người B

namespace DrawingServer
{
    public class Server
    {
        private TcpListener _listener = null!;
        public static ConcurrentDictionary<string, ClientSession> Clients = new ConcurrentDictionary<string, ClientSession>();

        // 10 màu cố định theo kế hoạch Tuần 2
        private readonly string[] _fixedColors = { "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FF00FF", "#00FFFF", "#800000", "#008000", "#000080", "#808000" };
        private int _colorIndex = 0;

        public async Task StartAsync()
        {
            _listener = new TcpListener(IPAddress.Any, 8888);
            _listener.Start();
            Console.WriteLine("TCP Server dang chay tren port 8888 (Async)...");

            // Chạy UDP Server song song
            UdpServer udpServer = new UdpServer();
            _ = Task.Run(() => udpServer.StartAsync());

            while (true)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Console.WriteLine($"[+] Client moi ket noi TCP: {client.Client.RemoteEndPoint}");
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Loi khi accept client: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            // Bắt null an toàn, nếu rỗng thì dùng Guid
            string clientId = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
            ClientSession session = new ClientSession(client);
            NetworkStream stream = client.GetStream();

            try
            {
                // Gán màu xoay vòng
                session.AssignedColor = _fixedColors[_colorIndex % 10];
                _colorIndex++;
                Clients.TryAdd(clientId, session);
                Console.WriteLine($"[INFO] Da gan mau {session.AssignedColor} cho {clientId}");

                // Gửi kích thước Canvas cho Client mới bằng Packet của B
                string canvasJson = "{\"Width\":1280, \"Height\":720}";
                Packet canvasPacket = new Packet
                {
                    Cmd = CommandType.CANVAS_SIZE,
                    Payload = Encoding.UTF8.GetBytes(canvasJson)
                };
                byte[] canvasData = canvasPacket.Serialize();
                await stream.WriteAsync(canvasData, 0, canvasData.Length);

                byte[] buffer = new byte[4096];
                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    // Nơi xử lý các lệnh TCP nhận từ Client (sẽ dùng nhiều ở Tuần 3)
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Loi client {clientId}: {ex.Message}");
            }
            finally
            {
                // Xóa khỏi danh sách khi disconnect
                Clients.TryRemove(clientId, out _);
                client.Close();
                Console.WriteLine($"[-] Client {clientId} da ngat ket noi.");

                // Thông báo USER_LEAVE cho người khác
                Packet leavePacket = new Packet
                {
                    Cmd = CommandType.USER_LEAVE,
                    Payload = Encoding.UTF8.GetBytes($"{{\"ClientId\":\"{clientId}\"}}")
                };
                await BroadcastTcpAsync(leavePacket.Serialize(), clientId);
            }
        }

        // Hàm hỗ trợ gửi TCP cho tất cả client (trừ người gửi)
        private async Task BroadcastTcpAsync(byte[] data, string excludeClientId)
        {
            foreach (var kvp in Clients)
            {
                if (kvp.Key != excludeClientId)
                {
                    try
                    {
                        await kvp.Value.TcpClient.GetStream().WriteAsync(data, 0, data.Length);
                    }
                    catch { }
                }
            }
        }
    }
}