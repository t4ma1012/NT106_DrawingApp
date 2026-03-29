// ============================================================
// DrawingClient/Network/ClientNetwork.cs
// Tuần 1→8 — TCP client hoàn chỉnh
// Xử lý tất cả CommandType, raise NetworkEvents
// ============================================================
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using SharedLib.Packets;
using SharedLib.Payloads;
using SharedLib.Security;
using SharedLib.Logging;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace DrawingClient.Network
{
    public class ClientNetwork
    {
        private TcpClient _tcpClient;
        private Stream _stream;       // SslStream sau Tuần 4
        private Thread _receiveThread;
        private Thread _heartbeatThread;  // Tuần 9: Heartbeat detection
        private volatile bool _running = false;
        
        // Heartbeat (Tuần 9)
        private long _lastHeartbeatReceived = 0;  // Unix ms
        private const int HEARTBEAT_INTERVAL_SEC = 30;     // Gửi HB mỗi 30s
        private const int HEARTBEAT_TIMEOUT_SEC = 10;      // Timeout sau 10s không nhận HB

        public string CurrentUsername { get; private set; }
        public string CurrentRoomCode { get; private set; }
        public bool IsConnected => _tcpClient?.Connected ?? false;

        // ── CONNECT / DISCONNECT ────────────────────────────────

        /// <summary>Kết nối TCP tới server. Tuần 4+: bọc SslStream.</summary>
        public bool Connect(string ip, int port = 8888, bool useSSL = true)
        {
            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.Connect(ip, port);

                if (useSSL)
                {
                    var ssl = new SslStream(_tcpClient.GetStream(), false,
                        (s, cert, chain, err) => true);   // chấp nhận self-signed
                    ssl.AuthenticateAsClient("DrawingServer",
                        null, SslProtocols.Tls12 | SslProtocols.Tls13, false);
                    _stream = ssl;
                    Logger.Info("ClientNetwork", "Kết nối SSL thành công.");
                }
                else
                {
                    _stream = _tcpClient.GetStream();
                    Logger.Info("ClientNetwork", "Kết nối TCP (không SSL).");
                }

                _running = true;
                _lastHeartbeatReceived = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                _receiveThread = new Thread(ReceiveLoop)
                { IsBackground = true, Name = "TCP-Recv" };
                _receiveThread.Start();
                
                // Tuần 9: Bắt đầu heartbeat thread
                _heartbeatThread = new Thread(HeartbeatLoop)
                { IsBackground = true, Name = "TCP-Heartbeat" };
                _heartbeatThread.Start();

                Logger.Info("ClientNetwork", "Kết nối TCP thành công, heartbeat started.");
                NetworkEvents.RaiseConnected();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("ClientNetwork", $"Lỗi kết nối: {ex.Message}");
                return false;
            }
        }

        // ── HEARTBEAT DETECTION (Tuần 9) ───────────────────────
        
        /// <summary>Gửi HEARTBEAT packet mỗi 30 giây để detect server down.</summary>
        private void HeartbeatLoop()
        {
            while (_running)
            {
                try
                {
                    // Chờ HEARTBEAT_INTERVAL_SEC giây trước khi gửi
                    for (int i = 0; i < HEARTBEAT_INTERVAL_SEC * 10; i++)
                    {
                        if (!_running) return;
                        Thread.Sleep(100);
                    }

                    // Gửi heartbeat
                    if (_running && IsConnected)
                    {
                        SendEmpty(CommandType.HEARTBEAT);
                        Logger.Debug("Heartbeat", $"HEARTBEAT sent.");
                    }

                    // Check timeout: nếu không nhận HB trong HEARTBEAT_TIMEOUT_SEC
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long timeSinceLastHb = (now - _lastHeartbeatReceived) / 1000;

                    if (timeSinceLastHb > HEARTBEAT_TIMEOUT_SEC)
                    {
                        Logger.Warning("Heartbeat", 
                            $"Server không phản hồi heartbeat trong {timeSinceLastHb}s. Reconnecting...");
                        _running = false;
                        NetworkEvents.RaiseDisconnected();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception("Heartbeat", ex);
                    if (_running)
                    {
                        _running = false;
                        NetworkEvents.RaiseDisconnected();
                    }
                }
            }
        }

        public void Disconnect()
        {
            _running = false;
            try { SendEmpty(CommandType.DISCONNECT); } catch { }
            _stream?.Close();
            _tcpClient?.Close();
            Logger.Info("ClientNetwork", "Disconnected.");
            NetworkEvents.RaiseDisconnected();
        }

        // ── SEND ────────────────────────────────────────────────

        public void Send(Packet packet)
        {
            if (_stream == null || !IsConnected)
            {
                Logger.Warning("ClientNetwork", "Gửi thất bại: stream null hoặc không connected");
                return;
            }
            try
            {
                byte[] data = packet.Serialize();
                // Prefix với 4 bytes độ dài (big-endian)
                byte[] lenBytes = BitConverter.GetBytes(data.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                lock (_stream)
                {
                    // Double-check stream still valid
                    if (_stream == null || !_running)
                        throw new IOException("Stream đóng trong quá trình ghi");

                    _stream.Write(lenBytes, 0, 4);
                    _stream.Write(data, 0, data.Length);
                    _stream.Flush();
                }
            }
            catch (IOException ioEx)
            {
                Logger.Error("ClientNetwork", $"IOException gửi packet: {ioEx.Message}");
                if (_running)
                {
                    _running = false;
                    NetworkEvents.RaiseDisconnected();
                }
            }
            catch (ObjectDisposedException dispEx)
            {
                Logger.Error("ClientNetwork", $"Stream đã bị dispose: {dispEx.Message}");
                if (_running)
                {
                    _running = false;
                    NetworkEvents.RaiseDisconnected();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ClientNetwork", $"Lỗi gửi packet ({ex.GetType().Name}): {ex.Message}");
                if (_running)
                {
                    _running = false;
                    NetworkEvents.RaiseDisconnected();
                }
            }
        }

        public void Send(CommandType cmd, object payload)
            => Send(PacketHelper.Create(cmd, payload));

        public void SendEmpty(CommandType cmd)
            => Send(PacketHelper.CreateEmpty(cmd));

        // ── AUTH (Tuần 3) ───────────────────────────────────────

        public void SendLogin(string username, string password)
        {
            CurrentUsername = username;
            Send(CommandType.LOGIN, new LoginPayload { Username = username, Password = password });
        }

        public void SendRegister(string username, string password)
        {
            Send(CommandType.REGISTER, new RegisterPayload { Username = username, Password = password });
        }

        // ── ROOM (Tuần 3) ───────────────────────────────────────

        public void SendCreateRoom(int canvasWidth = 1280, int canvasHeight = 720)
        {
            Send(CommandType.CREATE_ROOM, new CreateRoomPayload
            { CanvasWidth = canvasWidth, CanvasHeight = canvasHeight });
        }

        public void SendJoinRoom(string roomCode, bool isSpectator = false)
        {
            CurrentRoomCode = roomCode;
            Send(CommandType.JOIN_ROOM, new JoinRoomPayload
            { RoomCode = roomCode, IsSpectator = isSpectator });
        }

        public void SendLeaveRoom()
        {
            SendEmpty(CommandType.LEAVE_ROOM);
            CurrentRoomCode = null;
        }

        // ── SYNC / UNDO (Tuần 3) ────────────────────────────────

        public void SendSyncBoard()
            => Send(CommandType.SYNC_BOARD, new { RoomCode = CurrentRoomCode });

        public void SendUndo(string actionId)
            => Send(CommandType.UNDO, new UndoPayload { ActionID = actionId, Username = CurrentUsername });

        public void SendRedo(string actionId)
            => Send(CommandType.REDO, new RedoPayload { ActionID = actionId, Username = CurrentUsername });

        // ── CHAT / ACTIVITY (Tuần 3) ────────────────────────────

        public void SendChat(string message, int colorArgb = 0)
        {
            Send(CommandType.CHAT, new ChatPayload
            {
                Username = CurrentUsername,
                ColorARGB = colorArgb,
                Message = message,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        // ── CLAIM AREA (Tuần 3-4) ───────────────────────────────

        public void SendClaimArea(string claimId, int x1, int y1, int x2, int y2)
        {
            Send(CommandType.CLAIM_AREA, new ClaimAreaPayload
            {
                ClaimID = claimId, Username = CurrentUsername,
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, DurationSeconds = 30
            });
        }

        public void SendReleaseArea(string claimId)
        {
            Send(CommandType.RELEASE_AREA, new ReleaseAreaPayload
            { ClaimID = claimId, Username = CurrentUsername });
        }

        // ── GALLERY (Tuần 3-4) ──────────────────────────────────

        public void SendSaveToGallery(string filename, string imageBase64, string thumbBase64, bool isAi = false)
        {
            Send(CommandType.SAVE_TO_GALLERY, new SaveGalleryPayload
            {
                RoomCode = CurrentRoomCode, Username = CurrentUsername,
                Filename = filename, ImageData = imageBase64,
                ThumbnailData = thumbBase64, IsAiGenerated = isAi
            });
        }

        public void SendGetGallery()
            => Send(CommandType.GET_GALLERY, new GetGalleryPayload { RoomCode = CurrentRoomCode });

        // ── PLAYBACK (Tuần 4) ───────────────────────────────────

        public void SendRequestPlayback()
            => Send(CommandType.REQUEST_PLAYBACK, new PlaybackRequestPayload { RoomCode = CurrentRoomCode });

        // ── IMPORT IMAGE (Tuần 3) ────────────────────────────────

        public void SendImportImage(int x, int y, int w, int h, string imageBase64)
        {
            Send(CommandType.IMPORT_IMAGE, new ImportImagePayload
            {
                ActionID = Guid.NewGuid().ToString(),
                Username = CurrentUsername,
                X = x, Y = y, Width = w, Height = h,
                ImageData = imageBase64,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        // ── AI FEATURES (Tuần 5) ────────────────────────────────

        public void SendAiTextToImageResult(AiTextToImageResultPayload payload)
            => Send(CommandType.AI_TEXT_TO_IMAGE, payload);

        public void SendAiBgRemovedResult(AiBgRemovedPayload payload)
            => Send(CommandType.AI_BG_REMOVED, payload);

        // ── AI FEATURES (Tuần 6) ────────────────────────────────

        public void SendAiMagicEraseRequest(AiMagicEraseRequestPayload payload)
            => Send(CommandType.AI_MAGIC_ERASE, payload);

        public void SendAiAutoCompleteRequest(AiAutoCompleteRequestPayload payload)
            => Send(CommandType.AI_AUTOCOMPLETE, payload);

        // ── ADVANCED FEATURES (Tuần 5-6) ────────────────────────

        public void SendSticker(StickerPayload payload)
            => Send(CommandType.STICKER, payload);

        public void SendFollowMode(string targetUsername, bool isFollowing)
        {
            Send(CommandType.FOLLOW_MODE, new FollowModePayload
            { FollowerUsername = CurrentUsername, TargetUsername = targetUsername, IsFollowing = isFollowing });
        }

        public void SendStickyNote(StickyNotePayload payload)
            => Send(CommandType.STICKY_NOTE, payload);

        public void SendVote(string actionId)
        {
            Send(CommandType.VOTE_DRAW, new VoteDrawPayload
            { ActionID = actionId, VoterUsername = CurrentUsername });
        }

        public void SendTimelineRequest(long targetTimestamp)
        {
            Send(CommandType.TIMELINE_REQUEST, new TimelineRequestPayload
            { RoomCode = CurrentRoomCode, TargetTimestamp = targetTimestamp });
        }

        public void SendSnapshotList()
            => Send(CommandType.SNAPSHOT_LIST, new { RoomCode = CurrentRoomCode });

        public void SendSnapshotRestore(int snapshotId)
        {
            Send(CommandType.SNAPSHOT_RESTORE, new SnapshotRestorePayload
            { RoomCode = CurrentRoomCode, SnapshotID = snapshotId });
        }

        // ── GAMIFICATION (Tuần 7-8) ─────────────────────────────

        public void SendDrawingPrompt(string promptText, int countdownSeconds = 60)
        {
            Send(CommandType.DRAWING_PROMPT, new DrawingPromptPayload
            { PromptText = promptText, CountdownSeconds = countdownSeconds, IsStart = true });
        }

        public void SendBlindDrawStart()
        {
            Send(CommandType.BLIND_DRAW_START, new BlindDrawPayload
            { IsReveal = false, RoomCode = CurrentRoomCode });
        }

        public void SendBlindDrawReveal()
        {
            Send(CommandType.BLIND_DRAW_REVEAL, new BlindDrawPayload
            { IsReveal = true, RoomCode = CurrentRoomCode });
        }

        public void SendExportGifRequest(int fpsFrames = 10, long startTimestamp = 0, long endTimestamp = 0)
        {
            Send(CommandType.EXPORT_GIF_REQUEST, new GifExportRequestPayload
            {
                RoomCode = CurrentRoomCode,
                FpsFrames = fpsFrames,
                Filename = $"drawing_{System.DateTime.Now:yyyyMMdd_HHmmss}.gif",
                StartTimestamp = startTimestamp,
                EndTimestamp = endTimestamp
            });
        }

        public void SendPixelArtSync(PixelArtSyncPayload payload)
            => Send(CommandType.PIXEL_ART_SYNC, payload);

        // ── RECEIVE LOOP ────────────────────────────────────────

        private void ReceiveLoop()
        {
            byte[] lenBuf = new byte[4];
            while (_running)
            {
                try
                {
                    ReadExact(lenBuf, 4);
                    if (BitConverter.IsLittleEndian)
                    {
                        byte[] copy = new byte[4];
                        Array.Copy(lenBuf, copy, 4);
                        Array.Reverse(copy);
                        int packetLen = BitConverter.ToInt32(copy, 0);
                        byte[] packetBuf = new byte[packetLen];
                        ReadExact(packetBuf, packetLen);
                        var packet = Packet.Deserialize(packetBuf);
                        ProcessPacket(packet);
                    }
                    else
                    {
                        int packetLen = BitConverter.ToInt32(lenBuf, 0);
                        byte[] packetBuf = new byte[packetLen];
                        ReadExact(packetBuf, packetLen);
                        var packet = Packet.Deserialize(packetBuf);
                        ProcessPacket(packet);
                    }
                }
                catch (IOException) when (!_running) { break; }
                catch (Exception ex)
                {
                    Logger.Error("ClientNetwork", $"Receive error: {ex.Message}");
                    if (_running) NetworkEvents.RaiseDisconnected();
                    break;
                }
            }
        }

        private void ReadExact(byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = _stream.Read(buffer, total, count - total);
                if (read == 0) throw new IOException("Server đóng kết nối.");
                total += read;
            }
        }

        private void ProcessPacket(Packet p)
        {
            // Tuần 9: Update last heartbeat timestamp trên mọi packet
            if (p.Cmd != CommandType.HEARTBEAT)
            {
                _lastHeartbeatReceived = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            
            switch (p.Cmd)
            {
                // ── HEARTBEAT (Tuần 9) ──────────────────────────────
                case CommandType.HEARTBEAT:
                    _lastHeartbeatReceived = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    Logger.Debug("Heartbeat", "ACK received from server.");
                    break;

                // AUTH
                case CommandType.LOGIN_RESPONSE:
                    NetworkEvents.RaiseLoginResponse(PacketHelper.GetPayload<LoginResponse>(p));
                    break;
                case CommandType.REGISTER_RESPONSE:
                    NetworkEvents.RaiseRegisterResponse(PacketHelper.GetPayload<RegisterResponse>(p));
                    break;

                // ROOM
                case CommandType.CREATE_ROOM_RESPONSE:
                    NetworkEvents.RaiseCreateRoomResponse(PacketHelper.GetPayload<CreateRoomResponse>(p));
                    break;
                case CommandType.JOIN_ROOM_RESPONSE:
                    var jr = PacketHelper.GetPayload<JoinRoomResponse>(p);
                    if (jr.IsSuccess) CurrentRoomCode = jr.RoomCode;
                    NetworkEvents.RaiseJoinRoomResponse(jr);
                    break;
                case CommandType.ROOM_MEMBERS:
                    NetworkEvents.RaiseRoomMembersReceived(PacketHelper.GetPayload<RoomMembersPayload>(p));
                    break;
                case CommandType.USER_JOIN:
                    NetworkEvents.RaiseUserJoined(PacketHelper.GetPayload<UserJoinPayload>(p));
                    break;
                case CommandType.USER_LEAVE:
                    NetworkEvents.RaiseUserLeft(PacketHelper.GetPayload<UserLeavePayload>(p));
                    break;
                case CommandType.CANVAS_SIZE:
                    NetworkEvents.RaiseCanvasSizeReceived(PacketHelper.GetPayload<CanvasSizePayload>(p));
                    break;

                // SYNC / UNDO
                case CommandType.SYNC_BOARD:
                    NetworkEvents.RaiseSyncBoardReceived(PacketHelper.GetPayload<SyncBoardPayload>(p));
                    break;
                case CommandType.UNDO:
                    NetworkEvents.RaiseUndoReceived(PacketHelper.GetPayload<UndoPayload>(p));
                    break;
                case CommandType.REDO:
                    NetworkEvents.RaiseRedoReceived(PacketHelper.GetPayload<RedoPayload>(p));
                    break;
                case CommandType.PLAYBACK_RESPONSE:
                    NetworkEvents.RaisePlaybackReceived(PacketHelper.GetPayload<PlaybackResponsePayload>(p));
                    break;

                // TCP DRAW (import image, background)
                case CommandType.IMPORT_IMAGE:
                    NetworkEvents.RaiseImportImageReceived(PacketHelper.GetPayload<ImportImagePayload>(p));
                    break;
                case CommandType.SET_BACKGROUND:
                    NetworkEvents.RaiseSetBackgroundReceived(PacketHelper.GetPayload<SetBackgroundPayload>(p));
                    break;
                case CommandType.CLEAR_ALL:
                    NetworkEvents.RaiseClearAll();
                    break;

                // CHAT / ACTIVITY
                case CommandType.CHAT:
                    NetworkEvents.RaiseChatReceived(PacketHelper.GetPayload<ChatPayload>(p));
                    break;
                case CommandType.ACTIVITY_LOG:
                    NetworkEvents.RaiseActivityLogReceived(PacketHelper.GetPayload<ActivityLogPayload>(p));
                    break;

                // CLAIM
                case CommandType.CLAIM_AREA:
                    NetworkEvents.RaiseClaimAreaReceived(PacketHelper.GetPayload<ClaimAreaPayload>(p));
                    break;
                case CommandType.AREA_RELEASED:
                    NetworkEvents.RaiseReleaseAreaReceived(PacketHelper.GetPayload<ReleaseAreaPayload>(p));
                    break;

                // GALLERY
                case CommandType.GALLERY_RESPONSE:
                    NetworkEvents.RaiseGalleryReceived(PacketHelper.GetPayload<GalleryResponsePayload>(p));
                    break;
                case CommandType.PUBLIC_GALLERY_LINK:
                    NetworkEvents.RaisePublicLinkReceived(PacketHelper.GetPayload<PublicGalleryLinkPayload>(p));
                    break;

                // AI
                case CommandType.AI_TEXT_TO_IMAGE:
                    NetworkEvents.RaiseAiTextToImageResult(PacketHelper.GetPayload<AiTextToImageResultPayload>(p));
                    break;
                case CommandType.AI_BG_REMOVED:
                    NetworkEvents.RaiseAiBgRemovedResult(PacketHelper.GetPayload<AiBgRemovedPayload>(p));
                    break;
                case CommandType.AI_MAGIC_ERASE:
                    NetworkEvents.RaiseAiMagicEraseResult(PacketHelper.GetPayload<AiMagicEraseResultPayload>(p));
                    break;
                case CommandType.AI_AUTOCOMPLETE:
                    NetworkEvents.RaiseAiAutoCompleteResult(PacketHelper.GetPayload<AiAutoCompleteResultPayload>(p));
                    break;

                // ADVANCED
                case CommandType.STICKER:
                    NetworkEvents.RaiseStickerReceived(PacketHelper.GetPayload<StickerPayload>(p));
                    break;
                case CommandType.FOLLOW_MODE:
                    NetworkEvents.RaiseFollowModeReceived(PacketHelper.GetPayload<FollowModePayload>(p));
                    break;
                case CommandType.STICKY_NOTE:
                    NetworkEvents.RaiseStickyNoteReceived(PacketHelper.GetPayload<StickyNotePayload>(p));
                    break;
                case CommandType.STICKY_NOTE_REPLY:
                    NetworkEvents.RaiseStickyNoteReplyReceived(PacketHelper.GetPayload<StickyNoteReplyPayload>(p));
                    break;
                case CommandType.VOTE_RESPONSE:
                    NetworkEvents.RaiseVoteResponse(PacketHelper.GetPayload<VoteResponsePayload>(p));
                    break;
                case CommandType.TIMELINE_RESPONSE:
                    NetworkEvents.RaiseTimelineResponse(PacketHelper.GetPayload<TimelineResponsePayload>(p));
                    break;
                case CommandType.SNAPSHOT_LIST:
                    NetworkEvents.RaiseSnapshotListReceived(PacketHelper.GetPayload<SnapshotListPayload>(p));
                    break;

                // GAMIFICATION
                case CommandType.DRAWING_PROMPT:
                    NetworkEvents.RaiseDrawingPromptReceived(PacketHelper.GetPayload<DrawingPromptPayload>(p));
                    break;
                case CommandType.BLIND_DRAW_START:
                case CommandType.BLIND_DRAW_REVEAL:
                    NetworkEvents.RaiseBlindDrawReceived(PacketHelper.GetPayload<BlindDrawPayload>(p));
                    break;
                case CommandType.PIXEL_ART_SYNC:
                    NetworkEvents.RaisePixelArtSyncReceived(PacketHelper.GetPayload<PixelArtSyncPayload>(p));
                    break;
                case CommandType.EXPORT_GIF_PROGRESS:
                    NetworkEvents.RaiseGifExportProgress(PacketHelper.GetPayload<GifExportProgressPayload>(p));
                    break;

                case CommandType.DISCONNECT:
                    _running = false;
                    NetworkEvents.RaiseDisconnected();
                    break;

                default:
                    Logger.Warning("ClientNetwork", $"Unhandled TCP cmd: {p.Cmd}");
                    break;
            }
        }
    }
}
