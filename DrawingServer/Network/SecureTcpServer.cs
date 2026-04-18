// ============================================================
// DrawingServer/Network/SecureTcpServer.cs — FIX
// Thêm WriteLock (SemaphoreSlim) vào BroadcastToRoomAsync
// và SendPacketToClientAsync để tránh race condition trên SslStream
// Đây là nguyên nhân màu nền và sticker không đồng bộ được
// ============================================================
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using SharedLib.Packets;
using SharedLib.Payloads;
using SharedLib.Logging;
using DrawingServer.Services;
using DrawingServer.Database;

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
                await sslStream.AuthenticateAsServerAsync(_serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);
                session.SecureStream = sslStream;
                Clients.TryAdd(clientId, session);

                // Gửi canvas size ngay khi kết nối
                await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.CANVAS_SIZE, new CanvasSizePayload { Width = 1280, Height = 720 }));

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

                    switch (packet.Cmd)
                    {
                        case CommandType.HEARTBEAT:
                            await SendPacketToClientAsync(session, PacketHelper.CreateEmpty(CommandType.HEARTBEAT));
                            break;

                        case CommandType.LOGIN:
                            var loginData = PacketHelper.GetPayload<LoginPayload>(packet);
                            if (loginData != null)
                            {
                                var dbResult = await DbManager.LoginAsync(loginData.Username, loginData.Password);
                                await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.LOGIN_RESPONSE,
                                    new LoginResponse { IsSuccess = dbResult.IsSuccess, Message = dbResult.Message }));
                                if (dbResult.IsSuccess) session.Username = loginData.Username;
                            }
                            break;

                        case CommandType.CREATE_ROOM:
                            var roomData = PacketHelper.GetPayload<CreateRoomPayload>(packet);
                            var createResult = await RoomService.CreateRoomAsync(session.Username ?? "guest", roomData?.CanvasWidth ?? 1280, roomData?.CanvasHeight ?? 720);
                            await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.CREATE_ROOM_RESPONSE, new CreateRoomResponse
                            {
                                IsSuccess = createResult.Success,
                                RoomCode = createResult.RoomCode,
                                Message = createResult.Success ? "Tạo phòng thành công" : createResult.Message
                            }));
                            break;

                        case CommandType.JOIN_ROOM:
                            var joinData = PacketHelper.GetPayload<JoinRoomPayload>(packet);
                            if (joinData != null)
                            {
                                bool exists = await DbManager.CheckRoomExistsAsync(joinData.RoomCode);
                                await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.JOIN_ROOM_RESPONSE,
                                    new JoinRoomResponse { IsSuccess = exists, RoomCode = joinData.RoomCode, Message = exists ? "OK" : "Lỗi" }));

                                if (exists)
                                {
                                    session.RoomCode = joinData.RoomCode;
                                    try { await RoomService.AddMemberToRoomAsync(joinData.RoomCode, session); } catch { }
                                    try
                                    {
                                        var members = RoomService.GetRoomMembersInfo(joinData.RoomCode);
                                        await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.ROOM_MEMBERS,
                                            new RoomMembersPayload { RoomCode = joinData.RoomCode, Members = members }));
                                    }
                                    catch { }

                                    var history = await DbManager.GetRoomHistoryAsync(joinData.RoomCode);
                                    if (history.Count > 0)
                                    {
                                        string historyJson = "[" + string.Join(",", history) + "]";
                                        await SendPacketToClientAsync(session, new Packet { Cmd = CommandType.SYNC_BOARD, Payload = Encoding.UTF8.GetBytes(historyJson) });
                                    }
                                }
                            }
                            break;

                        case CommandType.LEAVE_ROOM:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                try { RoomService.RemoveMemberFromRoom(session.RoomCode, session.Username ?? "unknown"); } catch { }
                                await BroadcastToRoomAsync(session.RoomCode,
                                    PacketHelper.Create(CommandType.USER_LEAVE, new UserLeavePayload { Username = session.Username ?? "unknown" }),
                                    excludeClientId: clientId);
                                session.RoomCode = "";
                            }
                            break;

                        case CommandType.CHAT:
                            var chatData = PacketHelper.GetPayload<ChatPayload>(packet);
                            if (chatData != null && !string.IsNullOrEmpty(session.RoomCode))
                            {
                                chatData.Username = session.Username ?? "unknown";
                                packet.Payload = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(chatData));
                                try { await DbManager.SaveChatMessageAsync(session.RoomCode, chatData.Username, chatData.Message); } catch { }
                                await BroadcastToRoomAsync(session.RoomCode, packet, excludeClientId: clientId);
                            }
                            break;

                        // ── Broadcast cho TẤT CẢ trong phòng (kể cả người gửi) ──
                        case CommandType.UNDO:
                        case CommandType.REDO:
                        case CommandType.CLEAR_ALL:
                        case CommandType.SET_BACKGROUND:   // ✅ màu nền
                        case CommandType.IMPORT_IMAGE:
                        case CommandType.STICKER:          // ✅ sticker
                        case CommandType.STICKY_NOTE:
                        case CommandType.FOLLOW_MODE:
                        case CommandType.SET_TURNBASED:
                        case CommandType.CLAIM_AREA:
                        case CommandType.AI_TEXT_TO_IMAGE:
                        case CommandType.AI_BG_REMOVED:
                        case CommandType.AI_MAGIC_ERASE:
                        case CommandType.AI_AUTOCOMPLETE:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                                await BroadcastToRoomAsync(session.RoomCode, packet);
                            break;

                        case CommandType.SAVE_TO_GALLERY:
                            var galleryData = PacketHelper.GetPayload<SaveGalleryPayload>(packet);
                            if (galleryData != null && !string.IsNullOrEmpty(session.RoomCode))
                            {
                                bool saved = await DbManager.SaveGalleryItemAsync(session.RoomCode, galleryData.Filename, galleryData.ImageData, session.Username ?? "unknown");
                                await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.SAVE_TO_GALLERY,
                                    new SaveGalleryResponse { IsSuccess = saved, Message = saved ? "Đã lưu" : "Lỗi" }));
                            }
                            break;

                        case CommandType.EXPORT_GIF_REQUEST:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                _ = Task.Run(async () =>
                                {
                                    for (int i = 10; i <= 100; i += 30)
                                    {
                                        await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.GIF_EXPORT_PROGRESS, new GifExportProgressPayload
                                        {
                                            ProgressPercent = i,
                                            Status = i == 100 ? "completed" : "processing",
                                            GifData = i == 100 ? "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7" : ""
                                        }));
                                        await Task.Delay(800);
                                    }
                                });
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

        // ✅ Gửi packet đến 1 client — dùng WriteLock để tránh race condition
        private async Task SendPacketToClientAsync(ClientSession client, Packet packet)
        {
            if (client?.SecureStream == null) return;
            byte[] data = packet.Serialize();
            byte[] lenBytes = BitConverter.GetBytes(data.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);

            await client.WriteLock.WaitAsync();
            try
            {
                await client.SecureStream.WriteAsync(lenBytes, 0, 4);
                await client.SecureStream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Logger.Warning("TCP", $"Lỗi gửi đến {client.Username}: {ex.Message}");
            }
            finally
            {
                client.WriteLock.Release();
            }
        }

        // ✅ Broadcast đến tất cả client trong phòng — mỗi client dùng WriteLock riêng
        private async Task BroadcastToRoomAsync(string roomCode, Packet packet, string excludeClientId = "")
        {
            foreach (var kvp in Clients)
            {
                ClientSession client = kvp.Value;
                if (client.RoomCode != roomCode) continue;
                if (!string.IsNullOrEmpty(excludeClientId) && kvp.Key == excludeClientId) continue;
                await SendPacketToClientAsync(client, packet);
            }
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
