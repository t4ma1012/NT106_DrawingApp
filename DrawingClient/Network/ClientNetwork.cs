using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using SharedLib.Packets;
using SharedLib.Payloads;

namespace DrawingClient.Network
{
    public class ClientNetwork
    {
        // ── Kết nối TCP ──────────────────────────────────
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private Thread _receiveThread;
        private bool _isConnected = false;

        public string CurrentUsername { get; private set; }

        // ── Events — Người A đăng ký để nhận dữ liệu ────
        public event Action<DrawPayload> OnDrawReceived;
        public event Action<string, string> OnChatReceived;      // username, message
        public event Action<string, int> OnUserJoined;        // username, colorARGB
        public event Action<string> OnUserLeft;          // username
        public event Action<LoginResponse> OnLoginResponse;
        public event Action<JoinRoomResponse> OnJoinRoomResponse;
        public event Action<CreateRoomResponse> OnCreateRoomResponse;
        public event Action OnDisconnected;

        // ─────────────────────────────────────────────────
        // KẾT NỐI TỚI SERVER
        // ─────────────────────────────────────────────────
        public bool Connect(string ip, int port)
        {
            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.Connect(ip, port);
                _stream = _tcpClient.GetStream();
                _isConnected = true;

                // Bắt đầu thread lắng nghe dữ liệu từ server
                _receiveThread = new Thread(ReceiveLoop);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();

                Console.WriteLine($"[ClientNetwork] Kết nối thành công tới {ip}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientNetwork] Lỗi kết nối: {ex.Message}");
                return false;
            }
        }

        // ─────────────────────────────────────────────────
        // NGẮT KẾT NỐI
        // ─────────────────────────────────────────────────
        public void Disconnect()
        {
            _isConnected = false;
            _stream?.Close();
            _tcpClient?.Close();
            OnDisconnected?.Invoke();
        }

        // ─────────────────────────────────────────────────
        // GỬI PACKET QUA TCP
        // ─────────────────────────────────────────────────
        public void Send(Packet packet)
        {
            if (!_isConnected) return;

            try
            {
                byte[] data = packet.Serialize();
                _stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientNetwork] Lỗi gửi: {ex.Message}");
                Disconnect();
            }
        }

        // Shortcut: gửi với payload object luôn
        public void Send(CommandType cmd, object payload)
        {
            Send(PacketHelper.Create(cmd, payload));
        }

        // Shortcut: gửi không có payload
        public void SendEmpty(CommandType cmd)
        {
            Send(PacketHelper.CreateEmpty(cmd));
        }

        // ─────────────────────────────────────────────────
        // GỬI CÁC LỆNH CỤ THỂ — Người A gọi các hàm này
        // ─────────────────────────────────────────────────
        public void SendLogin(string username, string password)
        {
            CurrentUsername = username;
            Send(CommandType.LOGIN, new LoginPayload
            {
                Username = username,
                Password = password
            });
        }

        public void SendRegister(string username, string password)
        {
            Send(CommandType.REGISTER, new LoginPayload
            {
                Username = username,
                Password = password
            });
        }

        public void SendCreateRoom(int canvasWidth, int canvasHeight)
        {
            Send(CommandType.CREATE_ROOM, new CreateRoomPayload
            {
                CanvasWidth = canvasWidth,
                CanvasHeight = canvasHeight
            });
        }

        public void SendJoinRoom(string roomCode, bool isSpectator = false)
        {
            Send(CommandType.JOIN_ROOM, new JoinRoomPayload
            {
                RoomCode = roomCode,
                IsSpectator = isSpectator
            });
        }

        public void SendDraw(DrawPayload payload)
        {
            payload.Username = CurrentUsername;
            Send(CommandType.DRAW, payload);
        }

        public void SendChat(string message)
        {
            Send(CommandType.CHAT, new ChatPayload
            {
                Username = CurrentUsername,
                Message = message,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        public void SendCursor(int x, int y)
        {
            Send(CommandType.CURSOR, new CursorPayload
            {
                Username = CurrentUsername,
                X = x,
                Y = y
            });
        }

        public void SendLeaveRoom()
        {
            SendEmpty(CommandType.LEAVE_ROOM);
        }

        // ─────────────────────────────────────────────────
        // NHẬN DỮ LIỆU TỪ SERVER (chạy trong thread riêng)
        // ─────────────────────────────────────────────────
        private void ReceiveLoop()
        {
            byte[] headerBuf = new byte[6]; // Header(1)+CMD(1)+Length(4)

            while (_isConnected)
            {
                try
                {
                    // Đọc đúng 6 byte header trước
                    int bytesRead = 0;
                    while (bytesRead < 6)
                    {
                        int r = _stream.Read(headerBuf, bytesRead, 6 - bytesRead);
                        if (r == 0) throw new Exception("Server đóng kết nối");
                        bytesRead += r;
                    }

                    // Đọc Length từ byte 2-5
                    int payloadLen = (headerBuf[2] << 24) | (headerBuf[3] << 16)
                                   | (headerBuf[4] << 8) | headerBuf[5];

                    // Đọc Payload
                    byte[] payloadBuf = new byte[payloadLen];
                    int payloadRead = 0;
                    while (payloadRead < payloadLen)
                    {
                        int r = _stream.Read(payloadBuf, payloadRead, payloadLen - payloadRead);
                        if (r == 0) throw new Exception("Server đóng kết nối");
                        payloadRead += r;
                    }

                    // Ghép lại thành full packet
                    byte[] fullData = new byte[6 + payloadLen];
                    Array.Copy(headerBuf, 0, fullData, 0, 6);
                    Array.Copy(payloadBuf, 0, fullData, 6, payloadLen);

                    Packet packet = Packet.Deserialize(fullData);
                    HandlePacket(packet);
                }
                catch (Exception ex)
                {
                    if (_isConnected)
                    {
                        Console.WriteLine($"[ClientNetwork] Mất kết nối: {ex.Message}");
                        Disconnect();
                    }
                    break;
                }
            }
        }

        // ─────────────────────────────────────────────────
        // XỬ LÝ PACKET NHẬN ĐƯỢC — raise event cho Người A
        // ─────────────────────────────────────────────────
        private void HandlePacket(Packet packet)
        {
            switch (packet.Cmd)
            {
                case CommandType.DRAW:
                    var draw = PacketHelper.GetPayload<DrawPayload>(packet);
                    OnDrawReceived?.Invoke(draw);
                    break;

                case CommandType.CHAT:
                    var chat = PacketHelper.GetPayload<ChatPayload>(packet);
                    OnChatReceived?.Invoke(chat.Username, chat.Message);
                    break;

                case CommandType.USER_JOIN:
                    var join = PacketHelper.GetPayload<UserJoinPayload>(packet);
                    OnUserJoined?.Invoke(join.Username, join.ColorARGB);
                    break;

                case CommandType.USER_LEAVE:
                    var leave = PacketHelper.GetPayload<UserLeavePayload>(packet);
                    OnUserLeft?.Invoke(leave.Username);
                    break;

                case CommandType.LOGIN_RESPONSE:
                    var loginResp = PacketHelper.GetPayload<LoginResponse>(packet);
                    OnLoginResponse?.Invoke(loginResp);
                    break;

                case CommandType.JOIN_ROOM_RESPONSE:
                    var joinResp = PacketHelper.GetPayload<JoinRoomResponse>(packet);
                    OnJoinRoomResponse?.Invoke(joinResp);
                    break;

                case CommandType.CREATE_ROOM_RESPONSE:
                    var createResp = PacketHelper.GetPayload<CreateRoomResponse>(packet);
                    OnCreateRoomResponse?.Invoke(createResp);
                    break;

                default:
                    Console.WriteLine($"[ClientNetwork] Nhận CMD không xử lý: {packet.Cmd}");
                    break;
            }
        }
    }
}
