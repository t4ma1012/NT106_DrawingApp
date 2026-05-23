// ============================================================
// DrawingServer/Network/SecureTcpServer.cs — FIX
// Thêm WriteLock (SemaphoreSlim) vào BroadcastToRoomAsync
// và SendPacketToClientAsync để tránh race condition trên SslStream
// Đây là nguyên nhân màu nền và sticker không đồng bộ được
// ============================================================
using System;
using System.Linq;
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

                        case CommandType.REGISTER:
                            var regData = PacketHelper.GetPayload<RegisterPayload>(packet);
                            if (regData != null)
                            {
                                // Kiểm tra username đã tồn tại chưa
                                var existCheck = await DbManager.LoginAsync(regData.Username, regData.Password);
                                bool alreadyExists = existCheck.Message == "Sai mật khẩu!";

                                if (alreadyExists)
                                {
                                    await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.REGISTER_RESPONSE,
                                        new RegisterResponse { IsSuccess = false, Message = "Tên tài khoản đã tồn tại!" }));
                                }
                                else
                                {
                                    // LoginAsync tự tạo nếu chưa có → reuse
                                    var regResult = await DbManager.LoginAsync(regData.Username, regData.Password);
                                    await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.REGISTER_RESPONSE,
                                        new RegisterResponse { IsSuccess = regResult.IsSuccess, Message = regResult.IsSuccess ? "Đăng ký thành công! Hãy đăng nhập." : regResult.Message }));
                                }
                                Logger.Info("TCP", $"[REGISTER] '{regData.Username}' → {(alreadyExists ? "đã tồn tại" : "thành công")}");
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
                                if (!string.IsNullOrEmpty(session.RoomCode) &&
                                    !string.Equals(session.RoomCode, joinData.RoomCode, StringComparison.OrdinalIgnoreCase))
                                {
                                    try { RoomService.RemoveMemberFromRoom(session.RoomCode, session.Username ?? "unknown"); } catch { }
                                    session.RoomCode = "";
                                    session.UdpEndPoint = null;
                                }

                                bool exists = await DbManager.CheckRoomExistsAsync(joinData.RoomCode);
                                await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.JOIN_ROOM_RESPONSE,
                                    new JoinRoomResponse { IsSuccess = exists, RoomCode = joinData.RoomCode, Message = exists ? "OK" : "Lỗi" }));

                                if (exists)
                                {
                                    session.RoomCode = joinData.RoomCode;
                                    try { await RoomService.AddMemberToRoomAsync(joinData.RoomCode, session); } catch { }

                                    // Gamification: khởi tạo diem cho user moi join
                                    GamificationService.EnsureUser(joinData.RoomCode, session.Username ?? "guest");

                                    // Gui leaderboard hien tai cho user moi vao
                                    var lbEntries = GamificationService.GetLeaderboard(joinData.RoomCode);
                                    await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.LEADERBOARD,
                                        new SharedLib.Payloads.LeaderboardPayload
                                        {
                                            RoomCode = joinData.RoomCode,
                                            Entries = lbEntries.Select(e => new SharedLib.Payloads.LeaderboardEntry
                                            { Rank = e.Rank, Username = e.Username, Score = e.Score }).ToList()
                                        }));
                                    try
                                    {
                                        var members = RoomService.GetRoomMembersInfo(joinData.RoomCode);
                                        await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.ROOM_MEMBERS,
                                            new RoomMembersPayload { RoomCode = joinData.RoomCode, Members = members }));

                                        var roomState = RoomService.GetRoomState(joinData.RoomCode);
                                        if (roomState != null && roomState.IsTurnBasedEnabled)
                                        {
                                            await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.SET_TURNBASED,
                                                new TurnBasedPayload
                                                {
                                                    RoomCode = joinData.RoomCode,
                                                    IsEnabled = true,
                                                    ActiveUser = roomState.ActiveDrawingUser
                                                }));
                                        }
                                            
                                        // Broadcast USER_JOIN cho các client khác trong phòng
                                        await BroadcastToRoomAsync(joinData.RoomCode, PacketHelper.Create(CommandType.USER_JOIN, 
                                            new UserJoinPayload { Username = session.Username ?? "guest" }), excludeClientId: clientId);
                                    }
                                    catch { }

                                    var history = await DbManager.GetRoomHistoryAsync(joinData.RoomCode);
                                    Logger.Info("TCP", $"[JOIN] '{session.Username}' room={joinData.RoomCode} history={history.Count} strokes");
                                    if (history.Count > 0)
                                    {
                                        string historyJson = "[" + string.Join(",", history) + "]";
                                        await SendPacketToClientAsync(session, new Packet { Cmd = CommandType.SYNC_BOARD, Payload = Encoding.UTF8.GetBytes(historyJson) });
                                        Logger.Info("TCP", $"[SYNC_BOARD] Gửi {history.Count} strokes cho '{session.Username}'");
                                    }
                                    else
                                    {
                                        Logger.Warning("TCP", $"[JOIN] Không có history cho phòng {joinData.RoomCode} — canvas sẽ trắng");
                                    }
                                }
                            }
                            break;

                        case CommandType.LEAVE_ROOM:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                string leavingRoom = session.RoomCode;
                                try { RoomService.RemoveMemberFromRoom(session.RoomCode, session.Username ?? "unknown"); } catch { }
                                await BroadcastToRoomAsync(session.RoomCode,
                                    PacketHelper.Create(CommandType.USER_LEAVE, new UserLeavePayload { Username = session.Username ?? "unknown" }),
                                    excludeClientId: clientId);
                                session.RoomCode = "";
                                session.UdpEndPoint = null;
                                Logger.Info("TCP", $"[LEAVE] '{session.Username}' rời phòng {leavingRoom}");
                            }
                            break;

                        case CommandType.CHAT:
                            var chatData = PacketHelper.GetPayload<ChatPayload>(packet);
                            if (chatData != null && !string.IsNullOrEmpty(session.RoomCode))
                            {
                                chatData.Username = session.Username ?? "unknown";
                                packet.Payload = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(chatData));
                                try { await DbManager.SaveChatMessageAsync(session.RoomCode, chatData.Username, chatData.Message); } catch { }
                                Logger.Info("TCP", $"[CHAT] '{session.Username}' → room={session.RoomCode} msg='{chatData.Message}'");
                                await BroadcastToRoomAsync(session.RoomCode, packet);
                            }
                            else
                            {
                                Logger.Warning("TCP", $"[CHAT] Bỏ qua — Username='{session.Username}' RoomCode='{session.RoomCode}'");
                            }
                            break;

                        // ── Broadcast cho TẤT CẢ trong phòng (kể cả người gửi) ──
                        case CommandType.STICKER:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                if (IsTurnBlocked(session)) break;

                                // ✅ FIX: Lưu DB với ToolType="Sticker" để client biết cách replay khi reconnect
                                try
                                {
                                    string stickerJson = Encoding.UTF8.GetString(packet.Payload);
                                    var stickerObj = Newtonsoft.Json.Linq.JObject.Parse(stickerJson);
                                    stickerObj["ToolType"] = "Sticker";
                                    string actionId = stickerObj["ActionID"]?.ToString();
                                    actionId = string.IsNullOrWhiteSpace(actionId) ? Guid.NewGuid().ToString() : actionId;
                                    await DbManager.SaveStrokeAsync(session.RoomCode, actionId, stickerObj.ToString(), session.Username ?? "");
                                }
                                catch { }

                                await BroadcastToRoomAsync(session.RoomCode, packet, excludeClientId: clientId);
                                // Gamification: +3 diem khi dat sticker
                                int stickerScore = GamificationService.AddScore(session.RoomCode, session.Username ?? "", GamificationService.POINTS_STICKER);
                                await BroadcastToRoomAsync(session.RoomCode, PacketHelper.Create(CommandType.SCORE_UPDATE,
                                    new SharedLib.Payloads.ScoreUpdatePayload { RoomCode = session.RoomCode, Username = session.Username, Score = stickerScore, Delta = GamificationService.POINTS_STICKER, Reason = "sticker" }));
                            }
                            break;

                        case CommandType.UNDO:
                        case CommandType.REDO:
                        case CommandType.FOLLOW_MODE:
                        case CommandType.SET_TURNBASED:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                if ((packet.Cmd == CommandType.UNDO || packet.Cmd == CommandType.REDO) && IsTurnBlocked(session))
                                    break;

                                if (packet.Cmd == CommandType.SET_TURNBASED)
                                {
                                    var turnData = PacketHelper.GetPayload<TurnBasedPayload>(packet) ?? new TurnBasedPayload();
                                    var roomState = RoomService.GetRoomState(session.RoomCode);
                                    if (roomState != null)
                                    {
                                        roomState.IsTurnBasedEnabled = turnData.IsEnabled;
                                        roomState.ActiveDrawingUser = turnData.IsEnabled ? (session.Username ?? "") : "";
                                    }

                                    packet = PacketHelper.Create(CommandType.SET_TURNBASED, new TurnBasedPayload
                                    {
                                        RoomCode = session.RoomCode,
                                        Username = session.Username ?? "",
                                        IsEnabled = turnData.IsEnabled,
                                        ActiveUser = turnData.IsEnabled ? (session.Username ?? "") : ""
                                    });
                                    Logger.Info("TCP", $"[TURN_BASED] {(turnData.IsEnabled ? "ON" : "OFF")} active='{session.Username}' phòng {session.RoomCode}");
                                }

                                await BroadcastToRoomAsync(session.RoomCode, packet);
                            }
                            break;

                        case CommandType.CLAIM_AREA:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                                await BroadcastToRoomAsync(session.RoomCode, packet, excludeClientId: clientId);
                            break;

                        case CommandType.CLEAR_ALL:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                if (IsTurnBlocked(session)) break;

                                // Xóa lịch sử vẽ trong DB để người join sau không nhận history cũ
                                try { await DbManager.ClearRoomHistoryAsync(session.RoomCode); } catch { }
                                // Broadcast cho tất cả NGOẠI TRỪ người gửi (họ đã clear local rồi)
                                await BroadcastToRoomAsync(session.RoomCode, packet, excludeClientId: clientId);
                                Logger.Info("TCP", $"[CLEAR_ALL] '{session.Username}' xóa canvas phòng {session.RoomCode}");
                            }
                            break;

                        case CommandType.CANVAS_SIZE:
                            // ✅ Client thay đổi kích thước canvas → broadcast cho cả phòng
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                await BroadcastToRoomAsync(session.RoomCode, packet, excludeClientId: clientId);
                                Logger.Info("TCP", $"[CANVAS_SIZE] Broadcast từ '{session.Username}' phòng {session.RoomCode}");
                            }
                            break;

                        case CommandType.SET_BACKGROUND:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                if (IsTurnBlocked(session)) break;

                                // ✅ FIX: Lưu màu nền với ToolType="SetBackground" — khi reconnect canvas sẽ đúng màu nền
                                try
                                {
                                    string bgJson = Encoding.UTF8.GetString(packet.Payload);
                                    var bgObj = Newtonsoft.Json.Linq.JObject.Parse(bgJson);
                                    bgObj["ToolType"] = "SetBackground";
                                    string actionId = bgObj["ActionID"]?.ToString();
                                    actionId = string.IsNullOrWhiteSpace(actionId) ? Guid.NewGuid().ToString() : actionId;
                                    await DbManager.SaveStrokeAsync(session.RoomCode, actionId, bgObj.ToString(), session.Username ?? "");
                                }
                                catch { }
                                await BroadcastToRoomAsync(session.RoomCode, packet, excludeClientId: clientId);
                            }
                            break;

                        case CommandType.IMPORT_IMAGE:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                if (IsTurnBlocked(session)) break;

                                // ✅ FIX: Lưu ảnh import với ToolType="ImportImage" — khi reconnect ảnh vẫn còn trên canvas
                                try
                                {
                                    string imgJson = Encoding.UTF8.GetString(packet.Payload);
                                    var imgObj = Newtonsoft.Json.Linq.JObject.Parse(imgJson);
                                    imgObj["ToolType"] = "ImportImage";
                                    await DbManager.SaveStrokeAsync(session.RoomCode, Guid.NewGuid().ToString(), imgObj.ToString(), session.Username ?? "");
                                }
                                catch { }
                                await BroadcastToRoomAsync(session.RoomCode, packet, excludeClientId: clientId);
                            }
                            break;

                        case CommandType.STICKY_NOTE:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                                await BroadcastToRoomAsync(session.RoomCode, packet, excludeClientId: clientId);
                            break;

                        case CommandType.AI_TEXT_TO_IMAGE:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                var aiTtiData = PacketHelper.GetPayload<SharedLib.Payloads.AiTextToImageResultPayload>(packet);
                                if (aiTtiData != null)
                                    _ = DbManager.SaveAiResultAsync(session.RoomCode, "text_to_image",
                                        aiTtiData.Prompt ?? "", aiTtiData.ImageData ?? "", session.Username ?? "");
                                await BroadcastToRoomAsync(session.RoomCode, packet);
                            }
                            break;

                        case CommandType.AI_BG_REMOVED:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                var aiBgData = PacketHelper.GetPayload<SharedLib.Payloads.AiBgRemovedPayload>(packet);
                                if (aiBgData != null)
                                    _ = DbManager.SaveAiResultAsync(session.RoomCode, "bg_removed",
                                        "", aiBgData.ImageData ?? "", session.Username ?? "");
                                await BroadcastToRoomAsync(session.RoomCode, packet);
                            }
                            break;

                        case CommandType.AI_MAGIC_ERASE:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                var aiMeData = PacketHelper.GetPayload<SharedLib.Payloads.AiMagicEraseResultPayload>(packet);
                                if (aiMeData != null)
                                    _ = DbManager.SaveAiResultAsync(session.RoomCode, "magic_erase",
                                        "", aiMeData.ResultImageData ?? "", session.Username ?? "");
                                await BroadcastToRoomAsync(session.RoomCode, packet);
                            }
                            break;

                        case CommandType.AI_AUTOCOMPLETE:
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                var aiAcData = PacketHelper.GetPayload<SharedLib.Payloads.AiAutoCompleteResultPayload>(packet);
                                if (aiAcData != null)
                                    _ = DbManager.SaveAiResultAsync(session.RoomCode, "autocomplete",
                                        "", aiAcData.ResultImageData ?? "", session.Username ?? "");
                                await BroadcastToRoomAsync(session.RoomCode, packet);
                            }
                            break;

                        case CommandType.SAVE_TO_GALLERY:
                            var galleryData = PacketHelper.GetPayload<SaveGalleryPayload>(packet);
                            if (galleryData != null && !string.IsNullOrEmpty(session.RoomCode))
                            {
                                var (saved, galleryId, publicToken) = await DbManager.SaveGalleryItemAsync(
                                    session.RoomCode, galleryData.Filename, galleryData.ImageData, session.Username ?? "unknown");

                                // Tạo public URL từ token (format: /gallery/<token>)
                                string publicUrl = saved ? $"/gallery/{publicToken}" : "";

                                await SendPacketToClientAsync(session, PacketHelper.Create(CommandType.SAVE_TO_GALLERY,
                                    new SaveGalleryResponse
                                    {
                                        IsSuccess  = saved,
                                        Message    = saved ? "Đã lưu vào Gallery" : "Lỗi lưu Gallery",
                                        GalleryUrl = publicUrl
                                    }));

                                // Nếu lưu thành công, broadcast PUBLIC_GALLERY_LINK cho cả phòng
                                // để ai cũng thấy link chia sẻ mới
                                if (saved)
                                {
                                    await BroadcastToRoomAsync(session.RoomCode,
                                        PacketHelper.Create(CommandType.PUBLIC_GALLERY_LINK,
                                            new PublicGalleryLinkPayload
                                            {
                                                GalleryItemID = galleryId,
                                                PublicToken   = publicToken,
                                                PublicUrl     = publicUrl
                                            }));
                                    Logger.Info("TCP", $"[GALLERY] '{session.Username}' lưu '{galleryData.Filename}' → {publicUrl}");
                                }
                            }
                            break;

                        case CommandType.GET_GALLERY:
                            // Client xin danh sách gallery của phòng (mở tab Gallery)
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                var galleryItems = await DbManager.GetGalleryItemsAsync(session.RoomCode);
                                var galleryResp  = new GalleryResponsePayload
                                {
                                    RoomCode = session.RoomCode,
                                    Items    = galleryItems.Select(g => new GalleryItem
                                    {
                                        ID            = g.Id,
                                        Filename      = g.Filename,
                                        ThumbnailData = g.ImageData,   // client tự resize thumbnail
                                        SavedBy       = g.CreatedBy,
                                        SavedAt       = new DateTimeOffset(g.CreatedAt).ToUnixTimeMilliseconds(),
                                        PublicLink    = string.IsNullOrEmpty(g.PublicToken) ? "" : $"/gallery/{g.PublicToken}"
                                    }).ToList()
                                };
                                await SendPacketToClientAsync(session,
                                    PacketHelper.Create(CommandType.GALLERY_RESPONSE, galleryResp));
                                Logger.Info("TCP", $"[GALLERY] Gửi {galleryItems.Count} ảnh cho '{session.Username}'");
                            }
                            break;

                        case CommandType.PUBLIC_GALLERY_LINK:
                            // Client xin lấy nội dung 1 ảnh theo public token (xem ảnh public)
                            var linkReq = PacketHelper.GetPayload<PublicGalleryLinkPayload>(packet);
                            if (linkReq != null && !string.IsNullOrEmpty(linkReq.PublicToken))
                            {
                                var item = await DbManager.GetPublicGalleryItemAsync(linkReq.PublicToken);
                                if (item.HasValue)
                                {
                                    await SendPacketToClientAsync(session,
                                        PacketHelper.Create(CommandType.PUBLIC_GALLERY_LINK,
                                            new PublicGalleryLinkPayload
                                            {
                                                GalleryItemID = item.Value.Id,
                                                PublicToken   = linkReq.PublicToken,
                                                PublicUrl     = $"/gallery/{linkReq.PublicToken}"
                                            }));
                                }
                                else
                                {
                                    await SendPacketToClientAsync(session,
                                        PacketHelper.Create(CommandType.PUBLIC_GALLERY_LINK,
                                            new PublicGalleryLinkPayload { PublicToken = "", PublicUrl = "" }));
                                }
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

                        // ── PIXEL ART (TCP) ─────────────────────────────────────────
                        case CommandType.PIXEL_ART_SYNC:
                            // Client gửi PIXEL_ART_SYNC để xin toàn bộ board hiện tại
                            // (thường gọi ngay sau khi join phòng và chuyển sang chế độ Pixel Art)
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                var cells = await DbManager.GetPixelBoardAsync(session.RoomCode);
                                var syncPayload = new SharedLib.Payloads.PixelArtSyncPayload
                                {
                                    RoomCode = session.RoomCode,
                                    GridSize = 32,  // mặc định 32×32; client có thể override
                                    Cells    = cells.Select(c => new SharedLib.Payloads.PixelCell
                                    {
                                        Row      = c.Row,
                                        Col      = c.Col,
                                        ColorARGB = c.ColorArgb
                                    }).ToList()
                                };
                                await SendPacketToClientAsync(session,
                                    PacketHelper.Create(CommandType.PIXEL_ART_SYNC, syncPayload));
                                Logger.Info("TCP", $"[PIXEL_ART] Gửi {cells.Count} ô cho '{session.Username}' (phòng {session.RoomCode})");
                            }
                            break;

                        // ── SNAPSHOT + TIME TRAVEL (TCP) ────────────────────────────
                        case CommandType.SNAPSHOT_LIST:
                            // Client mở panel Snapshot → xin danh sách các mốc đã lưu
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                var snapshots = await DbManager.GetSnapshotListAsync(session.RoomCode);
                                var snapPayload = new SharedLib.Payloads.SnapshotListPayload
                                {
                                    RoomCode  = session.RoomCode,
                                    Snapshots = snapshots.Select(s => new SharedLib.Payloads.SnapshotInfo
                                    {
                                        SnapshotID      = s.Id,
                                        Timestamp       = new DateTimeOffset(s.TakenAt).ToUnixTimeMilliseconds(),
                                        ThumbnailBase64 = s.Thumbnail
                                    }).ToList()
                                };
                                await SendPacketToClientAsync(session,
                                    PacketHelper.Create(CommandType.SNAPSHOT_LIST, snapPayload));
                                Logger.Info("TCP", $"[SNAP] Gửi {snapshots.Count} snapshot cho '{session.Username}'");
                            }
                            break;

                        case CommandType.SNAPSHOT_RESTORE:
                            // Client chọn 1 snapshot → server gửi lại toàn bộ strokes của snapshot đó
                            var restoreReq = PacketHelper.GetPayload<SharedLib.Payloads.SnapshotRestorePayload>(packet);
                            if (restoreReq != null && !string.IsNullOrEmpty(session.RoomCode))
                            {
                                string snapData = await DbManager.GetSnapshotDataAsync(restoreReq.SnapshotID);
                                if (!string.IsNullOrEmpty(snapData))
                                {
                                    // Gửi lại như SYNC_BOARD để client replay
                                    await SendPacketToClientAsync(session,
                                        new Packet { Cmd = CommandType.SYNC_BOARD,
                                                     Payload = Encoding.UTF8.GetBytes(snapData) });
                                    Logger.Info("TCP", $"[SNAP] Restore snapshot #{restoreReq.SnapshotID} cho '{session.Username}'");
                                }
                            }
                            break;

                        case CommandType.REQUEST_PLAYBACK:
                            // Client yêu cầu phát lại toàn bộ lịch sử vẽ của phòng
                            if (!string.IsNullOrEmpty(session.RoomCode))
                            {
                                var pbHistory = await DbManager.GetRoomHistoryAsync(session.RoomCode);
                                if (pbHistory.Count > 0)
                                {
                                    // Gửi lại dưới dạng SYNC_BOARD để client replay (tái sử dụng handler có sẵn)
                                    string pbJson = "[" + string.Join(",", pbHistory) + "]";
                                    await SendPacketToClientAsync(session,
                                        new Packet { Cmd = CommandType.SYNC_BOARD, Payload = Encoding.UTF8.GetBytes(pbJson) });
                                    Logger.Info("TCP", $"[PLAYBACK] Gửi {pbHistory.Count} stroke cho '{session.Username}' phòng {session.RoomCode}");
                                }
                                else
                                {
                                    Logger.Info("TCP", $"[PLAYBACK] Không có history cho phòng {session.RoomCode}");
                                }
                            }
                            break;

                        case CommandType.TIMELINE_REQUEST:
                            // Client kéo thanh timeline → xin strokes đến thời điểm TargetTimestamp
                            var timelineReq = PacketHelper.GetPayload<SharedLib.Payloads.TimelineRequestPayload>(packet);
                            if (timelineReq != null && !string.IsNullOrEmpty(session.RoomCode))
                            {
                                var historyUntil = await DbManager.GetHistoryUntilAsync(
                                    session.RoomCode, timelineReq.TargetTimestamp);

                                string histJson = "[" + string.Join(",", historyUntil) + "]";
                                var tlResp = new SharedLib.Payloads.TimelineResponsePayload
                                {
                                    RoomCode        = session.RoomCode,
                                    TargetTimestamp = timelineReq.TargetTimestamp,
                                    Actions         = new System.Collections.Generic.List<SharedLib.Payloads.DrawAction>()
                                };
                                // Gửi raw JSON qua SYNC_BOARD để client replay đến đúng mốc thời gian
                                await SendPacketToClientAsync(session,
                                    PacketHelper.Create(CommandType.TIMELINE_RESPONSE, tlResp));
                                // Đồng thời gửi data thực qua SYNC_BOARD
                                if (historyUntil.Count > 0)
                                    await SendPacketToClientAsync(session,
                                        new Packet { Cmd = CommandType.SYNC_BOARD,
                                                     Payload = Encoding.UTF8.GetBytes(histJson) });
                                Logger.Info("TCP", $"[TIMELINE] Gửi {historyUntil.Count} stroke đến timestamp {timelineReq.TargetTimestamp} cho '{session.Username}'");
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
                if (!string.IsNullOrEmpty(session.RoomCode))
                {
                    try { 
                        RoomService.RemoveMemberFromRoom(session.RoomCode, session.Username ?? "unknown"); 
                        _ = BroadcastToRoomAsync(session.RoomCode, PacketHelper.Create(CommandType.USER_LEAVE, new UserLeavePayload { Username = session.Username ?? "unknown" }), excludeClientId: clientId);
                    } catch { }
                }
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
            int count = 0;
            foreach (var kvp in Clients)
            {
                ClientSession client = kvp.Value;
                if (client.RoomCode != roomCode) continue;
                if (!string.IsNullOrEmpty(excludeClientId) && kvp.Key == excludeClientId) continue;
                await SendPacketToClientAsync(client, packet);
                count++;
            }
            if (packet.Cmd == CommandType.CHAT)
            {
                Logger.Info("TCP", $"[CHAT BROADCAST] Gửi đến {count} client trong phòng {roomCode}");
                // Debug: log tất cả client và RoomCode của họ
                foreach (var kvp in Clients)
                    Logger.Info("TCP", $"  → Client '{kvp.Value.Username}' RoomCode='{kvp.Value.RoomCode}' excluded={kvp.Key == excludeClientId}");
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

        private bool IsTurnBlocked(ClientSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.RoomCode))
                return false;

            var roomState = RoomService.GetRoomState(session.RoomCode);
            if (roomState == null || !roomState.IsTurnBasedEnabled)
                return false;

            bool blocked = !string.Equals(roomState.ActiveDrawingUser, session.Username, StringComparison.OrdinalIgnoreCase);
            if (blocked)
                Logger.Info("TCP", $"[TURN_BASED] Chặn thao tác từ '{session.Username}', lượt hiện tại='{roomState.ActiveDrawingUser}'");
            return blocked;
        }
    }
}
