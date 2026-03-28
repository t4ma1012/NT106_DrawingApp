using System.Text.Json;
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
                    if (bytesRead == 0) break; // Client ngắt kết nối an toàn

                    try
                    {
                        // 1. Cắt lấy đúng phần dữ liệu thực tế nhận được
                        byte[] receivedData = new byte[bytesRead];
                        Array.Copy(buffer, receivedData, bytesRead);

                        // Dùng PacketDef của B để mở gói tin
                        Packet packet = Packet.Deserialize(receivedData);

                        // 2. Xử lý lệnh Đăng nhập (LOGIN)
                        if (packet.Cmd == CommandType.LOGIN)
                        {
                            string jsonPayload = Encoding.UTF8.GetString(packet.Payload);
                            var loginData = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(jsonPayload);

                            if (loginData != null && loginData.ContainsKey("Username") && loginData.ContainsKey("Password"))
                            {
                                string user = loginData["Username"];
                                string pass = loginData["Password"];

                                // Gọi hàm xuống DB để kiểm tra
                                var dbResult = await Database.DbManager.LoginAsync(user, pass);

                                // Chuẩn bị gói tin phản hồi
                                string responseJson = $"{{\"IsSuccess\": {dbResult.IsSuccess.ToString().ToLower()}, \"Message\": \"{dbResult.Message}\"}}";
                                Packet responsePacket = new Packet
                                {
                                    Cmd = CommandType.LOGIN_RESPONSE,
                                    Payload = Encoding.UTF8.GetBytes(responseJson)
                                };

                                // Gửi kết quả về cho Client
                                byte[] responseData = responsePacket.Serialize();
                                await stream.WriteAsync(responseData, 0, responseData.Length);

                                // Lưu Username vào Session nếu thành công
                                if (dbResult.IsSuccess)
                                {
                                    session.Username = user;
                                    Console.WriteLine($"[Auth] Client {clientId} dang nhap thanh cong voi ten '{user}'");
                                }
                                else
                                {
                                    Console.WriteLine($"[Auth] Client {clientId} dang nhap that bai: {dbResult.Message}");
                                }
                            }
                        }
                        // 3. Xử lý lệnh Tạo phòng (CREATE_ROOM)
                        else if (packet.Cmd == CommandType.CREATE_ROOM)
                        {
                            string jsonPayload = Encoding.UTF8.GetString(packet.Payload);
                            // Dùng Dictionary tạm để linh hoạt đọc JSON
                            var roomData = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, int>>(jsonPayload);

                            int width = roomData != null && roomData.ContainsKey("CanvasWidth") ? roomData["CanvasWidth"] : 1280;
                            int height = roomData != null && roomData.ContainsKey("CanvasHeight") ? roomData["CanvasHeight"] : 720;

                            // Trích xuất Username từ Session (người vừa đăng nhập)
                            string currentUser = session.Username ?? "guest";

                            // Lưu xuống DB và lấy mã phòng
                            string roomCode = await Database.DbManager.CreateRoomAsync(currentUser, width, height);

                            if (!string.IsNullOrEmpty(roomCode))
                            {
                                // Chuẩn bị gói tin trả về chứa mã phòng
                                string responseJson = $"{{\"RoomCode\": \"{roomCode}\", \"Message\": \"Tạo phòng thành công\"}}";
                                Packet responsePacket = new Packet
                                {
                                    Cmd = CommandType.CREATE_ROOM_RESPONSE,
                                    Payload = Encoding.UTF8.GetBytes(responseJson)
                                };

                                byte[] responseData = responsePacket.Serialize();
                                await stream.WriteAsync(responseData, 0, responseData.Length);

                                Console.WriteLine($"[Room] Client '{currentUser}' da tao phong: {roomCode}");
                            }
                        }

                        // 4. Xử lý lệnh Vào phòng (JOIN_ROOM)
                        else if (packet.Cmd == CommandType.JOIN_ROOM)
                        {
                            string jsonPayload = Encoding.UTF8.GetString(packet.Payload);
                            var joinData = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(jsonPayload);

                            if (joinData != null && joinData.ContainsKey("RoomCode"))
                            {
                                string roomCode = joinData["RoomCode"];
                                bool exists = await Database.DbManager.CheckRoomExistsAsync(roomCode);

                                string responseJson;
                                if (exists)
                                {
                                    responseJson = $"{{\"IsSuccess\": true, \"Message\": \"Vào phòng thành công\"}}";
                                    Console.WriteLine($"[Room] Client '{session.Username}' da vao phong: {roomCode}");
                                }
                                else
                                {
                                    responseJson = $"{{\"IsSuccess\": false, \"Message\": \"Phòng không tồn tại\"}}";
                                    Console.WriteLine($"[Room] Client '{session.Username}' vao phong THAT BAI: {roomCode}");
                                }

                                Packet responsePacket = new Packet
                                {
                                    Cmd = CommandType.JOIN_ROOM_RESPONSE,
                                    Payload = Encoding.UTF8.GetBytes(responseJson)
                                };

                                byte[] responseData = responsePacket.Serialize();
                                await stream.WriteAsync(responseData, 0, responseData.Length);
                            }
                        }
                        // Lệnh REGISTER, CREATE_ROOM, JOIN_ROOM sẽ thêm ở đây sau...
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TCP Parse Error] Loi khi doc packet tu {clientId}: {ex.Message}");
                    }
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