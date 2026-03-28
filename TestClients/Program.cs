using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestClients
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== BOT TEST: LOGIN -> CREATE ROOM -> JOIN ROOM ===");
            try
            {
                TcpClient client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", 8888);
                Console.WriteLine("[+] Ket noi TCP voi Server thanh cong!");

                NetworkStream stream = client.GetStream();

                // 1. TEST ĐĂNG NHẬP
                Console.WriteLine("\n--- 1. TEST LOGIN ---");
                await SendPacket(stream, 0x01, "{\"Username\":\"trung\", \"Password\":\"123456\"}");
                await ReceivePacket(stream); // Hứng gói 0x41 (Canvas Size)
                await ReceivePacket(stream); // Hứng gói 0x03 (Login Response)

                // 2. TEST TẠO PHÒNG
                Console.WriteLine("\n--- 2. TEST CREATE ROOM ---");
                // Giả sử mã lệnh CMD_CREATE_ROOM là 0x04 (sửa lại số này nếu nhóm quy định khác)
                await SendPacket(stream, 0x10, "{\"CanvasWidth\": 1280, \"CanvasHeight\": 720}");

                string createRoomRes = await ReceivePacket(stream);

                // Trích xuất RoomCode từ cục JSON Server trả về
                string roomCode = "";
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(createRoomRes))
                    {
                        roomCode = doc.RootElement.GetProperty("RoomCode").GetString() ?? "";
                    }
                }
                catch { }

                // 3. TEST VÀO PHÒNG
                if (!string.IsNullOrEmpty(roomCode))
                {
                    Console.WriteLine($"\n--- 3. TEST JOIN ROOM ({roomCode}) ---");
                    // Giả sử mã lệnh CMD_JOIN_ROOM là 0x06
                    await SendPacket(stream, 0x12, $"{{\"RoomCode\": \"{roomCode}\"}}");
                    await ReceivePacket(stream);
                }

                Console.WriteLine("\n[+] Hoan tat test. Bam phim bat ky de thoat...");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Loi: {ex.Message}");
            }
        }

        // Hàm hỗ trợ gói và gửi dữ liệu nhanh
        static async Task SendPacket(NetworkStream stream, byte cmd, string jsonPayload)
        {
            byte[] payload = Encoding.UTF8.GetBytes(jsonPayload);
            byte[] packetData = new byte[6 + payload.Length];
            packetData[0] = 0xFF; // Header
            packetData[1] = cmd;  // Lệnh

            byte[] lenBytes = BitConverter.GetBytes(payload.Length);
            packetData[2] = lenBytes[3]; packetData[3] = lenBytes[2];
            packetData[4] = lenBytes[1]; packetData[5] = lenBytes[0];

            Array.Copy(payload, 0, packetData, 6, payload.Length);
            await stream.WriteAsync(packetData, 0, packetData.Length);
            Console.WriteLine($"[Send] CMD: 0x{cmd:X2} | Data: {jsonPayload}");
        }

        // Hàm hỗ trợ đọc và in dữ liệu Server trả về
        static async Task<string> ReceivePacket(NetworkStream stream)
        {
            byte[] buffer = new byte[4096];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead >= 6 && buffer[0] == 0xFF)
            {
                byte cmd = buffer[1];
                int len = (buffer[2] << 24) | (buffer[3] << 16) | (buffer[4] << 8) | buffer[5];
                string response = Encoding.UTF8.GetString(buffer, 6, len);
                Console.WriteLine($"[Recv] CMD: 0x{cmd:X2} | JSON: {response}");
                return response;
            }
            return "";
        }
    }
}