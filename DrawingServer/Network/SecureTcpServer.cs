using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using SharedLib.Packets;
using SharedLib.Payloads;
using SharedLib.Logging;
using DrawingServer.Network;
using DrawingServer.Services;
using DrawingServer.Database; // Dùng DbManager

namespace DrawingServer.Network
{
    public class SecureTcpServer
    {
        private TcpListener _listener = null!;
        private X509Certificate2 _serverCertificate = null!;
        public static ConcurrentDictionary<string, ClientSession> Clients = new ConcurrentDictionary<string, ClientSession>();

        public async Task StartAsync(string pfxPath, string pfxPassword)
        {
            try
            {
                // Load chứng chỉ SSL
                _serverCertificate = new X509Certificate2(pfxPath, pfxPassword);
                _listener = new TcpListener(IPAddress.Any, 8888);
                _listener.Start();
                Logger.Info("TCP", "Secure TCP Server đang chạy trên port 8888 (TLS 1.2)...");

                while (true)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Logger.Info("TCP", $"[+] Client mới: {client.Client.RemoteEndPoint}");
                    _ = Task.Run(() => HandleClientAsync(client));
                }
            }
            catch (Exception ex)
            {
                Logger.Error("TCP", $"Lỗi khởi động Server: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(TcpClient tcpClient)
        {
            string clientId = tcpClient.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
            ClientSession session = new ClientSession(tcpClient);
            SslStream sslStream = new SslStream(tcpClient.GetStream(), false);

            try
            {
                // Bắt tay bảo mật TLS
                await sslStream.AuthenticateAsServerAsync(_serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);

                // Gắn ống bảo mật vào session để dùng cho Broadcast
                session.SecureStream = sslStream;
                Clients.TryAdd(clientId, session);

                // Gửi kích thước Canvas cho Client mới
                Packet canvasPacket = PacketHelper.Create(CommandType.CANVAS_SIZE, new CanvasSizePayload { Width = 1280, Height = 720 });
                await SendPacketAsync(sslStream, canvasPacket);

                while (true)
                {
                    byte[] lenBuf = new byte[4];
                    if (!await ReadExactAsync(sslStream, lenBuf, 4)) break;

                    if (BitConverter.IsLittleEndian) Array.Reverse(lenBuf);
                    int packetLen = BitConverter.ToInt32(lenBuf, 0);
                    if (packetLen <= 0 || packetLen > 5000000) break;

                    byte[] packetBuf = new byte[packetLen];
                    if (!await ReadExactAsync(sslStream, packetBuf, packetLen)) break;

                    Packet packet = Packet.Deserialize(packetBuf);

                    // XỬ LÝ LỆNH TỪ CLIENT
                    switch (packet.Cmd)
                    {
                        case CommandType.HEARTBEAT:
                            await SendPacketAsync(sslStream, PacketHelper.CreateEmpty(CommandType.HEARTBEAT));
                            break;

                        case CommandType.LOGIN:
                            var loginData = PacketHelper.GetPayload<LoginPayload>(packet);
                            if (loginData != null)
                            {
                                var dbResult = await DbManager.LoginAsync(loginData.Username, loginData.Password);
                                var resPayload = new LoginResponse { IsSuccess = dbResult.IsSuccess, Message = dbResult.Message };
                                await SendPacketAsync(sslStream, PacketHelper.Create(CommandType.LOGIN_RESPONSE, resPayload));

                                if (dbResult.IsSuccess) session.Username = loginData.Username;
                            }
                            break;

                        case CommandType.CREATE_ROOM:
                            var roomData = PacketHelper.GetPayload<CreateRoomPayload>(packet);
                            string roomCode = await DbManager.CreateRoomAsync(session.Username ?? "guest", roomData?.CanvasWidth ?? 1280, roomData?.CanvasHeight ?? 720);
                            if (roomCode != null)
                            {
                                await SendPacketAsync(sslStream, PacketHelper.Create(CommandType.CREATE_ROOM_RESPONSE, new CreateRoomResponse { IsSuccess = true, RoomCode = roomCode, Message = "Tạo phòng thành công" }));
                            }
                            break;

                        case CommandType.JOIN_ROOM:
                            var joinData = PacketHelper.GetPayload<JoinRoomPayload>(packet);
                            if (joinData != null)
                            {
                                bool exists = await DbManager.CheckRoomExistsAsync(joinData.RoomCode);
                                await SendPacketAsync(sslStream, PacketHelper.Create(CommandType.JOIN_ROOM_RESPONSE, new JoinRoomResponse { IsSuccess = exists, RoomCode = joinData.RoomCode, Message = exists ? "OK" : "Lỗi" }));

                                if (exists)
                                {
                                    session.RoomCode = joinData.RoomCode;

                                    // Bỏ qua lỗi nếu RoomService chưa hoàn thiện
                                    try { await RoomService.AddMemberToRoomAsync(joinData.RoomCode, session); } catch { }
                                    try
                                    {
                                        var members = RoomService.GetRoomMembersInfo(joinData.RoomCode);
                                        await SendPacketAsync(sslStream, PacketHelper.Create(CommandType.ROOM_MEMBERS, new RoomMembersPayload { RoomCode = joinData.RoomCode, Members = members }));
                                    }
                                    catch { }

                                    // Đồng bộ lịch sử vẽ
                                    var history = await DbManager.GetRoomHistoryAsync(joinData.RoomCode);
                                    if (history.Count > 0)
                                    {
                                        string historyJson = "[" + string.Join(",", history) + "]";
                                        await SendPacketAsync(sslStream, new Packet { Cmd = CommandType.SYNC_BOARD, Payload = Encoding.UTF8.GetBytes(historyJson) });
                                    }
                                }
                            }
                            break;

                        case CommandType.LEAVE_ROOM:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                try { RoomService.RemoveMemberFromRoom(session.RoomCode, session.Username ?? "unknown"); } catch { }
                                var leavePayload = new UserLeavePayload { Username = session.Username ?? "unknown" };

                                await BroadcastToRoomAsync(session.RoomCode, PacketHelper.Create(CommandType.USER_LEAVE, leavePayload), clientId);
                                session.RoomCode = "";
                                Logger.Info("TCP", $"User {session.Username} rời phòng.");
                            }
                            break;

                        case CommandType.CHAT:
                            var chatData = PacketHelper.GetPayload<ChatPayload>(packet);
                            if (chatData != null && !string.IsNullOrEmpty(session.RoomCode))
                            {
                                await DbManager.SaveChatMessageAsync(session.RoomCode, session.Username ?? "unknown", chatData.Message);
                                await BroadcastToRoomAsync(session.RoomCode, packet, clientId);
                            }
                            break;

                        case CommandType.UNDO:
                        case CommandType.REDO:
                        case CommandType.CLEAR_ALL: // Thay cho CLEAR_CANVAS
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                await BroadcastToRoomAsync(session.RoomCode, packet, clientId);
                            }
                            break;

                        case CommandType.SAVE_TO_GALLERY:
                            var galleryData = PacketHelper.GetPayload<SaveGalleryPayload>(packet);
                            if (galleryData != null && !string.IsNullOrEmpty(session.RoomCode))
                            {
                                bool saved = await DbManager.SaveGalleryItemAsync(session.RoomCode, galleryData.Filename, galleryData.ImageData, session.Username ?? "unknown");
                                var response = new SaveGalleryResponse { IsSuccess = saved, Message = saved ? "Đã lưu" : "Lỗi" };
                                await SendPacketAsync(sslStream, PacketHelper.Create(CommandType.SAVE_TO_GALLERY, response));
                            }
                            break;

                        // AI Features
                        case CommandType.AI_TEXT_TO_IMAGE:
                        case CommandType.AI_BG_REMOVED:
                        case CommandType.AI_MAGIC_ERASE:
                        case CommandType.AI_AUTOCOMPLETE:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                await BroadcastToRoomAsync(session.RoomCode, packet, clientId);
                            }
                            break;

                        default:
                            Logger.Warning("TCP", $"Bỏ qua lệnh chưa hỗ trợ: {packet.Cmd}");
                            break;
                    }
                }
            }
            catch (Exception ex) { Logger.Warning("TCP", $"Client disconnect: {ex.Message}"); }
            finally
            {
                try { RoomService.RemoveMemberFromRoom(session.RoomCode, session.Username ?? "unknown"); } catch { }
                try { AuthService.LogoutUser(session.Username ?? "unknown"); } catch { }
                Clients.TryRemove(clientId, out _);
                tcpClient.Close();
                Logger.Info("TCP", $"[-] Client {clientId} đã thoát.");
            }
        }

        // --- HÀM BROADCAST XỊN XÒ ---
        private async Task BroadcastToRoomAsync(string roomCode, Packet packet, string exclusionClientId = "")
        {
            try
            {
                byte[] data = packet.Serialize();
                byte[] lenBytes = BitConverter.GetBytes(data.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);

                foreach (var kvp in Clients)
                {
                    string currentId = kvp.Key;
                    ClientSession client = kvp.Value;

                    // Chỉ gửi cho người cùng phòng và bỏ qua người vừa gửi (exclusionClientId)
                    if (client.RoomCode == roomCode && currentId != exclusionClientId)
                    {
                        if (client.SecureStream != null)
                        {
                            try
                            {
                                await client.SecureStream.WriteAsync(lenBytes, 0, 4);
                                await client.SecureStream.WriteAsync(data, 0, data.Length);
                            }
                            catch (Exception ex)
                            {
                                Logger.Warning("TCP", $"Lỗi gửi gói tin đến {currentId}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("TCP", $"Lỗi tổng ở hàm Broadcast: {ex.Message}");
            }
        }

        private async Task SendPacketAsync(SslStream stream, Packet packet)
        {
            byte[] data = packet.Serialize();
            byte[] lenBytes = BitConverter.GetBytes(data.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
            await stream.WriteAsync(lenBytes, 0, 4);
            await stream.WriteAsync(data, 0, data.Length);
        }

        private async Task<bool> ReadExactAsync(SslStream stream, byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = await stream.ReadAsync(buffer, total, count - total);
                if (read == 0) return false;
                total += read;
            }
            return true;
        }
    }
}