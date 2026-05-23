// ============================================================
// DrawingClient/Network/UdpManager.cs — FIX v5
// GIẢI PHÁP ĐÚNG: Dùng 1 UdpClient duy nhất cho cả GỬI lẫn NHẬN
// - Bind 1 port cố định (ví dụ: hệ thống tự cấp khi bind port 0)
// - Gửi: dùng socket đó gửi đến server
// - Nhận: dùng cùng socket đó nhận về
// => Server broadcast về đúng port nguồn => nhận được!
// ============================================================
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharedLib.Packets;
using SharedLib.Payloads;
using SharedLib.Security;
using Newtonsoft.Json;

namespace DrawingClient.Network
{
    public class UdpManager : IDisposable
    {
        private UdpClient _socket;          // 1 socket duy nhất cho cả gửi lẫn nhận
        private CancellationTokenSource _cts;
        private Task _listenTask;
        private readonly string _serverIp;
        private readonly int _serverPort;
        private bool _disposed = false;

        public int LocalPort { get; private set; }

        public UdpManager(string serverIp = "127.0.0.1", int serverPort = 8889)
        {
            _serverIp = serverIp;
            _serverPort = serverPort;
        }

        public void Start()
        {
            // Bind port 0 → hệ thống tự cấp 1 port trống
            _socket = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            LocalPort = ((IPEndPoint)_socket.Client.LocalEndPoint).Port;

            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoop(_cts.Token));

            Console.WriteLine($"[UdpManager] Khởi động trên port {LocalPort}");
        }

        // ── GỬI ─────────────────────────────────────────────

        private void SendPacket(CommandType cmd, object payload)
        {
            try
            {
                string json = JsonConvert.SerializeObject(payload);
                byte[] payloadBytes = Encoding.UTF8.GetBytes(json);
                Packet packet = new Packet { Cmd = cmd, Payload = payloadBytes };
                byte[] encrypted = AesHelper.Encrypt(packet.Serialize());
                _socket.Send(encrypted, encrypted.Length, _serverIp, _serverPort);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UdpManager] Lỗi gửi: {ex.Message}");
            }
        }

        public void SendDraw(DrawPayload p) => SendPacket(CommandType.DRAW, p);
        public void SendFloodFill(FloodFillPayload p) => SendPacket(CommandType.FLOOD_FILL, p);
        public void SendSetBackground(SetBackgroundPayload p) => SendPacket(CommandType.SET_BACKGROUND, p);
        public void SendSticker(StickerPayload p) => SendPacket(CommandType.STICKER, p);
        public void SendChat(ChatPayload p) => SendPacket(CommandType.CHAT, p);
        public void SendTurnBased(TurnBasedPayload p) => SendPacket(CommandType.SET_TURNBASED, p);
        public void SendCursor(CursorPayload p) => SendPacket(CommandType.CURSOR, p);
        public void SendLaser(LaserPayload p) => SendPacket(CommandType.LASER, p);
        public void SendReaction(ReactionPayload p) => SendPacket(CommandType.REACTION, p);

        // ── NHẬN ────────────────────────────────────────────

        private void ListenLoop(CancellationToken token)
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    byte[] data = _socket.Receive(ref remoteEP);
                    ProcessPacket(data);
                }
                catch { break; }
            }
        }

        private void ProcessPacket(byte[] data)
        {
            try
            {
                byte[] decrypted = AesHelper.Decrypt(data);
                Packet packet = Packet.Deserialize(decrypted);
                string json = Encoding.UTF8.GetString(packet.Payload);

                switch (packet.Cmd)
                {
                    case CommandType.DRAW:
                    case CommandType.TEXT:
                        NetworkEvents.RaiseDrawReceived(
                            JsonConvert.DeserializeObject<DrawPayload>(json));
                        break;
                    case CommandType.FLOOD_FILL:
                        NetworkEvents.RaiseFloodFillReceived(
                            JsonConvert.DeserializeObject<FloodFillPayload>(json));
                        break;
                    case CommandType.SET_BACKGROUND:
                        NetworkEvents.RaiseSetBackgroundReceived(
                            JsonConvert.DeserializeObject<SetBackgroundPayload>(json));
                        break;
                    case CommandType.STICKER:
                        NetworkEvents.RaiseStickerReceived(
                            JsonConvert.DeserializeObject<StickerPayload>(json));
                        break;
                    case CommandType.CHAT:
                        NetworkEvents.RaiseChatReceived(
                            JsonConvert.DeserializeObject<ChatPayload>(json));
                        break;
                    case CommandType.SET_TURNBASED:
                    case CommandType.TURN_CHANGE:
                        NetworkEvents.RaiseTurnBasedReceived(
                            JsonConvert.DeserializeObject<TurnBasedPayload>(json));
                        break;
                    case CommandType.CURSOR:
                        NetworkEvents.RaiseCursorReceived(
                            JsonConvert.DeserializeObject<CursorPayload>(json));
                        break;
                    case CommandType.LASER:
                        NetworkEvents.RaiseLaserReceived(
                            JsonConvert.DeserializeObject<LaserPayload>(json));
                        break;
                    case CommandType.REACTION:
                        NetworkEvents.RaiseReactionReceived(
                            JsonConvert.DeserializeObject<ReactionPayload>(json));
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UdpManager] Lỗi xử lý packet: {ex.Message}");
            }
        }

        // ── CLEANUP ──────────────────────────────────────────

        public void Stop()
        {
            _cts?.Cancel();
            _socket?.Close();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _socket?.Dispose();
                _cts?.Dispose();
                _disposed = true;
            }
        }
    }
}
