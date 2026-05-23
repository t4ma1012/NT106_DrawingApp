using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using SharedLib.Packets;
using SharedLib.Payloads;
using SharedLib.Logging;
using System.Net.Security;
using System.Security.Authentication;
using Newtonsoft.Json;

namespace DrawingClient.Network
{
    public class ClientNetwork
    {
        private TcpClient _tcpClient;
        private Stream _stream;
        private Thread _receiveThread;
        private Thread _heartbeatThread;
        private volatile bool _running = false;

        // Heartbeat
        private long _lastHeartbeatReceived = 0;
        private const int HEARTBEAT_INTERVAL_SEC = 30;
        private const int HEARTBEAT_TIMEOUT_SEC = 10;

        public string CurrentUsername { get; private set; }
        public string CurrentRoomCode { get; private set; }
        public string ServerIp { get; private set; } = "127.0.0.1";
        public int ServerTcpPort { get; private set; } = 8888;
        public int ServerUdpPort { get; private set; } = 8889;
        public bool IsConnected => _tcpClient?.Connected ?? false;

        // ── CONNECT / DISCONNECT ────────────────────────────────

        public bool Connect(string ip, int port = 8888, bool useSSL = true)
        {
            try
            {
                ServerIp = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();
                ServerTcpPort = port;
                _tcpClient = new TcpClient();
                _tcpClient.Connect(ServerIp, port);

                if (useSSL)
                {
                    var ssl = new SslStream(_tcpClient.GetStream(), false, (s, cert, chain, err) => true);
                    ssl.AuthenticateAsClient("DrawingServer", null, SslProtocols.Tls12, false);
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

                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "TCP-Recv" };
                _receiveThread.Start();

                _heartbeatThread = new Thread(HeartbeatLoop) { IsBackground = true, Name = "TCP-Heartbeat" };
                _heartbeatThread.Start();

                NetworkEvents.RaiseConnected();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("ClientNetwork", $"Lỗi kết nối: {ex.Message}");
                return false;
            }
        }

        private void HeartbeatLoop()
        {
            while (_running)
            {
                try
                {
                    for (int i = 0; i < HEARTBEAT_INTERVAL_SEC * 10; i++)
                    {
                        if (!_running) return;
                        Thread.Sleep(100);
                    }

                    if (_running && IsConnected)
                    {
                        SendEmpty(CommandType.HEARTBEAT);
                    }

                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long timeSinceLastHb = (now - _lastHeartbeatReceived) / 1000;

                    if (timeSinceLastHb > HEARTBEAT_TIMEOUT_SEC)
                    {
                        Logger.Warning("Heartbeat", "Timeout Server. Reconnecting...");
                        _running = false;
                        NetworkEvents.RaiseDisconnected();
                        return;
                    }
                }
                catch
                {
                    if (_running) { _running = false; NetworkEvents.RaiseDisconnected(); }
                }
            }
        }

        public void Disconnect()
        {
            _running = false;
            try { SendEmpty(CommandType.DISCONNECT); } catch { }
            _stream?.Close();
            _tcpClient?.Close();
            NetworkEvents.RaiseDisconnected();
        }

        // ── SEND ────────────────────────────────────────────────

        public void Send(Packet packet)
        {
            if (_stream == null || !IsConnected) return;
            try
            {
                byte[] data = packet.Serialize();
                byte[] lenBytes = BitConverter.GetBytes(data.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                lock (_stream)
                {
                    if (_stream == null || !_running) return;
                    _stream.Write(lenBytes, 0, 4);
                    _stream.Write(data, 0, data.Length);
                    _stream.Flush();
                }
            }
            catch
            {
                if (_running) { _running = false; NetworkEvents.RaiseDisconnected(); }
            }
        }

        public void Send(CommandType cmd, object payload) => Send(PacketHelper.Create(cmd, payload));
        public void SendEmpty(CommandType cmd) => Send(PacketHelper.CreateEmpty(cmd));

        // ── GỬI TÍN HIỆU CÁC TÍNH NĂNG ──────────────────────────

        public void SendLogin(string username, string password)
        {
            CurrentUsername = username;
            Send(CommandType.LOGIN, new LoginPayload { Username = username, Password = password });
        }
        public void SendRegister(string username, string password) => Send(CommandType.REGISTER, new RegisterPayload { Username = username, Password = password });
        public void SendCreateRoom(int canvasWidth = 1280, int canvasHeight = 720) => Send(CommandType.CREATE_ROOM, new CreateRoomPayload { CanvasWidth = canvasWidth, CanvasHeight = canvasHeight });
        public void SendJoinRoom(string roomCode, bool isSpectator = false)
        {
            CurrentRoomCode = roomCode;
            Send(CommandType.JOIN_ROOM, new JoinRoomPayload { RoomCode = roomCode, IsSpectator = isSpectator });
        }
        public void SendLeaveRoom() { SendEmpty(CommandType.LEAVE_ROOM); CurrentRoomCode = null; }

        public void SendSyncBoard() => Send(CommandType.SYNC_BOARD, new { RoomCode = CurrentRoomCode });
        public void SendUndo(string actionId) => Send(CommandType.UNDO, new UndoPayload { ActionID = actionId, Username = CurrentUsername });
        public void SendRedo(string actionId) => Send(CommandType.REDO, new RedoPayload { ActionID = actionId, Username = CurrentUsername });
        public void SendChat(string message, int colorArgb = 0) => Send(CommandType.CHAT, new ChatPayload { Username = CurrentUsername, ColorARGB = colorArgb, Message = message, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        public void SendSticker(StickerPayload payload) => Send(CommandType.STICKER, payload);
        public void SendStickyNote(StickyNotePayload payload) => Send(CommandType.STICKY_NOTE, payload);
        public void SendFollowMode(string targetUsername, bool isFollowing) => Send(CommandType.FOLLOW_MODE, new FollowModePayload { FollowerUsername = CurrentUsername, TargetUsername = targetUsername, IsFollowing = isFollowing });
        public void SendExportGifRequest(int fpsFrames = 10, long startTimestamp = 0, long endTimestamp = 0) => Send(CommandType.EXPORT_GIF_REQUEST, new GifExportRequestPayload { RoomCode = CurrentRoomCode, FpsFrames = fpsFrames, Filename = $"drawing_{DateTime.Now:yyyyMMdd_HHmmss}.gif", StartTimestamp = startTimestamp, EndTimestamp = endTimestamp });
        public void SendGetGallery() => Send(CommandType.GET_GALLERY, new GetGalleryPayload { RoomCode = CurrentRoomCode });
        public void SendSaveGallery(string filename, string imageData, string thumbnailData) => Send(CommandType.SAVE_TO_GALLERY, new SaveGalleryPayload { RoomCode = CurrentRoomCode, Username = CurrentUsername, Filename = filename, ImageData = imageData, ThumbnailData = thumbnailData });

        // ── RECEIVE LOOP BỌC THÉP ───────────────────────────────

        private void ReceiveLoop()
        {
            byte[] lenBuf = new byte[4];
            while (_running)
            {
                try
                {
                    ReadExact(lenBuf, 4);
                    if (BitConverter.IsLittleEndian) Array.Reverse(lenBuf);

                    int packetLen = BitConverter.ToInt32(lenBuf, 0);
                    if (packetLen <= 0 || packetLen > 5000000) break; // Chặn rác tránh tràn RAM

                    byte[] packetBuf = new byte[packetLen];
                    ReadExact(packetBuf, packetLen);

                    var packet = Packet.Deserialize(packetBuf);
                    ProcessPacket(packet);
                }
                catch (IOException) when (!_running) { break; }
                catch (Exception) { if (_running) NetworkEvents.RaiseDisconnected(); break; }
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
            if (p.Cmd != CommandType.HEARTBEAT)
                _lastHeartbeatReceived = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // BỌC TRY-CATCH ĐỂ 1 LỆNH LỖI KHÔNG LÀM CHẾT TOÀN BỘ APP
            try
            {
                switch (p.Cmd)
                {
                    case CommandType.HEARTBEAT:
                        _lastHeartbeatReceived = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        break;

                    case CommandType.LOGIN_RESPONSE:
                        NetworkEvents.RaiseLoginResponse(PacketHelper.GetPayload<LoginResponse>(p));
                        break;
                    case CommandType.REGISTER_RESPONSE:
                        NetworkEvents.RaiseRegisterResponse(PacketHelper.GetPayload<RegisterResponse>(p));
                        break;
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

                    // FIX LỖI ĐỒNG BỘ MẠNH NHẤT TẠI ĐÂY (SYNC_BOARD)
                    case CommandType.SYNC_BOARD:
                        string jsonSync = Encoding.UTF8.GetString(p.Payload);
                        if (jsonSync.TrimStart().StartsWith("["))
                        {
                            try
                            {
                                // Parse từng item theo ToolType để xử lý đúng format
                                var rawItems = JsonConvert.DeserializeObject<List<Newtonsoft.Json.Linq.JObject>>(jsonSync);
                                if (rawItems != null && rawItems.Count > 0)
                                {
                                    var actions = new System.Collections.Generic.List<DrawAction>();
                                    foreach (var item in rawItems)
                                    {
                                        string toolType = item["ToolType"]?.ToString() ?? item["toolType"]?.ToString() ?? "";

                                        if (toolType.Equals("SetBackground", StringComparison.OrdinalIgnoreCase))
                                        {
                                            actions.Add(new DrawAction
                                            {
                                                ToolType  = "SetBackground",
                                                ColorARGB = item["ColorARGB"]?.ToObject<int>() ?? 0
                                            });
                                        }
                                        else if (toolType.Equals("ImportImage", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // ImportImagePayload dùng X/Y, map sang X1/Y1
                                            actions.Add(new DrawAction
                                            {
                                                ToolType    = "ImportImage",
                                                X1          = item["X"]?.ToObject<int>() ?? item["X1"]?.ToObject<int>() ?? 0,
                                                Y1          = item["Y"]?.ToObject<int>() ?? item["Y1"]?.ToObject<int>() ?? 0,
                                                ImageWidth  = item["Width"]?.ToObject<int>() ?? 400,
                                                ImageHeight = item["Height"]?.ToObject<int>() ?? 300,
                                                ImageData   = item["ImageData"]?.ToString() ?? ""
                                            });
                                        }
                                        else if (toolType.Equals("Sticker", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // StickerPayload: StickerID map sang Text, X/Y map sang X1/Y1
                                            actions.Add(new DrawAction
                                            {
                                                ToolType    = "Sticker",
                                                Text        = item["StickerID"]?.ToString() ?? "",
                                                X1          = item["X"]?.ToObject<int>() ?? 0,
                                                Y1          = item["Y"]?.ToObject<int>() ?? 0,
                                                ImageWidth  = item["Width"]?.ToObject<int>() ?? 64,
                                                ImageHeight = item["Height"]?.ToObject<int>() ?? 64,
                                            });
                                        }
                                        else
                                        {
                                            // DrawPayload thông thường (Pen, Line, FloodFill, Text, Spray...)
                                            var dp = item.ToObject<DrawPayload>();
                                            if (dp != null)
                                            {
                                                actions.Add(new DrawAction
                                                {
                                                    ToolType  = dp.ToolType,
                                                    X1        = dp.X1,
                                                    Y1        = dp.Y1,
                                                    X2        = dp.X2,
                                                    Y2        = dp.Y2,
                                                    ColorARGB = dp.ColorARGB,
                                                    Thickness = dp.Thickness,
                                                    Text      = dp.Text,
                                                    FontName  = dp.FontName,
                                                    FontSize  = dp.FontSize
                                                });
                                            }
                                        }
                                    }
                                    NetworkEvents.RaiseSyncBoardReceived(new SyncBoardPayload { Actions = actions });
                                    break;
                                }
                            }
                            catch { }
                            // Fallback: deserialize thành DrawAction (format cũ)
                            var fallbackActions = JsonConvert.DeserializeObject<List<DrawAction>>(jsonSync);
                            NetworkEvents.RaiseSyncBoardReceived(new SyncBoardPayload { Actions = fallbackActions });
                        }
                        else
                        {
                            NetworkEvents.RaiseSyncBoardReceived(PacketHelper.GetPayload<SyncBoardPayload>(p));
                        }
                        break;

                    case CommandType.UNDO:
                        NetworkEvents.RaiseUndoReceived(PacketHelper.GetPayload<UndoPayload>(p));
                        break;
                    case CommandType.REDO:
                        NetworkEvents.RaiseRedoReceived(PacketHelper.GetPayload<RedoPayload>(p));
                        break;
                    case CommandType.SET_BACKGROUND:
                        NetworkEvents.RaiseSetBackgroundReceived(PacketHelper.GetPayload<SetBackgroundPayload>(p));
                        break;
                    case CommandType.CLEAR_ALL:
                        NetworkEvents.RaiseClearAll();
                        break;
                    case CommandType.CHAT:
                        NetworkEvents.RaiseChatReceived(PacketHelper.GetPayload<ChatPayload>(p));
                        break;
                    case CommandType.STICKER:
                        NetworkEvents.RaiseStickerReceived(PacketHelper.GetPayload<StickerPayload>(p));
                        break;
                    case CommandType.STICKY_NOTE:
                        NetworkEvents.RaiseStickyNoteReceived(PacketHelper.GetPayload<StickyNotePayload>(p));
                        break;
                    case CommandType.FOLLOW_MODE:
                        NetworkEvents.RaiseFollowModeReceived(PacketHelper.GetPayload<FollowModePayload>(p));
                        break;
                    case CommandType.SET_TURNBASED:
                    case CommandType.TURN_CHANGE:
                        NetworkEvents.RaiseTurnBasedReceived(PacketHelper.GetPayload<TurnBasedPayload>(p));
                        break;
                    case CommandType.SAVE_TO_GALLERY:
                        NetworkEvents.RaiseSaveGalleryResponse(PacketHelper.GetPayload<SaveGalleryResponse>(p));
                        break;
                    case CommandType.GALLERY_RESPONSE:
                        NetworkEvents.RaiseGalleryReceived(PacketHelper.GetPayload<GalleryResponsePayload>(p));
                        break;
                    case CommandType.IMPORT_IMAGE:
                        NetworkEvents.RaiseImportImageReceived(PacketHelper.GetPayload<ImportImagePayload>(p));
                        break;
                    case CommandType.GIF_EXPORT_PROGRESS:
                        NetworkEvents.RaiseGifExportProgress(PacketHelper.GetPayload<GifExportProgressPayload>(p));
                        break;
                    case CommandType.DISCONNECT:
                        _running = false;
                        NetworkEvents.RaiseDisconnected();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("ClientNetwork", $"Lỗi bỏ qua khi xử lý lệnh {p.Cmd}: {ex.Message}");
            }
        }
    }
}
