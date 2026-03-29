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
            Console.WriteLine("=== BOT TEST: ĐỒNG BỘ NÉT VẼ (TUẦN 4) ===");
            try
            {
                TcpClient client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", 8888);
                Console.WriteLine("[+] Ket noi TCP voi Server thanh cong!");

                NetworkStream stream = client.GetStream();

                // 1. LOGIN
                Console.WriteLine("\n--- 1. TEST LOGIN ---");
                await SendPacket(stream, 0x01, "{\"Username\":\"trung\", \"Password\":\"123456\"}");
                await ReceivePacket(stream); // Hứng gói 0x41 (Canvas Size)
                await ReceivePacket(stream); // Hứng gói 0x03 (Login Response)

                // 2. CREATE ROOM
                Console.WriteLine("\n--- 2. TEST CREATE ROOM ---");
                await SendPacket(stream, 0x10, "{\"CanvasWidth\": 1280, \"CanvasHeight\": 720}");

                string createRoomRes = await ReceivePacket(stream);
                string roomCode = "";
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(createRoomRes))
                    {
                        roomCode = doc.RootElement.GetProperty("RoomCode").GetString() ?? "";
                    }
                }
                catch { }

                // 3. JOIN ROOM
                if (!string.IsNullOrEmpty(roomCode))
                {
                    Console.WriteLine($"\n--- 3. TEST JOIN ROOM ({roomCode}) ---");
                    await SendPacket(stream, 0x12, $"{{\"RoomCode\": \"{roomCode}\"}}");
                    await ReceivePacket(stream); // Hứng gói JOIN_ROOM_RESPONSE
                }

                // 4. BẮN NÉT VẼ BẰNG UDP (CMD: 0x30)
                Console.WriteLine("\n--- 4. TEST GUI NET VE (UDP) ---");
                UdpClient udpClient = new UdpClient();
                // Giả lập 1 nét vẽ có tọa độ nháp
                string drawJson = $"{{\"RoomCode\": \"{roomCode}\", \"Username\": \"trung\", \"ActionId\": \"{Guid.NewGuid()}\", \"StrokeData\": \"[10, 20]\"}}";
                byte[] drawPayload = Encoding.UTF8.GetBytes(drawJson);

                byte[] udpPacket = new byte[6 + drawPayload.Length];
                udpPacket[0] = 0xFF;
                udpPacket[1] = 0x30; // 0x30 là lệnh DRAW
                byte[] lenBytes = BitConverter.GetBytes(drawPayload.Length);
                udpPacket[2] = lenBytes[3]; udpPacket[3] = lenBytes[2]; udpPacket[4] = lenBytes[1]; udpPacket[5] = lenBytes[0];
                Array.Copy(drawPayload, 0, udpPacket, 6, drawPayload.Length);

                await udpClient.SendAsync(udpPacket, udpPacket.Length, "127.0.0.1", 8889);
                Console.WriteLine($"[Send UDP] CMD: 0x30 | Data: {drawJson}");

                // Đợi nửa giây cho Server kịp lưu nét vẽ vào DB PostgreSQL
                await Task.Delay(500);

                // 5. TEST ĐỒNG BỘ: VÀO LẠI PHÒNG ĐỂ XEM CÓ LẤY ĐƯỢC NÉT VẼ KHÔNG
                Console.WriteLine("\n--- 5. TEST SYNC (VAO LAI PHONG) ---");
                await SendPacket(stream, 0x12, $"{{\"RoomCode\": \"{roomCode}\"}}");
                await ReceivePacket(stream); // Nhận JOIN_ROOM_RESPONSE

                // NẾU CODE CHUẨN, BẠN SẼ NHẬN THÊM GÓI NÀY Ở CỬA SỔ CONSOLE:
                await ReceivePacket(stream); // Nhận SYNC_BOARD (0x40) chứa nét vẽ ở trên

                Console.WriteLine("\n[+] Hoan tat test. Bam phim bat ky de thoat...");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Loi: {ex.Message}");
            }
        }

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