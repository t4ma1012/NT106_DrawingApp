using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharedLib.Packets;
using SharedLib.Payloads;
using Newtonsoft.Json;

namespace DrawingClient.Network
{
    // ═══════════════════════════════════════════════════════
    // LẮNG NGHE GÓI UDP TỪ SERVER, DESERIALIZE VÀ RAISE EVENT
    // Chạy trên background thread — KHÔNG động vào UI trực tiếp
    // Người A dùng this.Invoke() trong handler để vẽ lên canvas
    // ═══════════════════════════════════════════════════════
    public class UdpReceiver : IDisposable
    {
        private UdpClient _udpClient;
        private CancellationTokenSource _cts;
        private Task _listenTask;
        private readonly int _port;
        private bool _disposed = false;

        // localPort: port client lắng nghe, mặc định 8889
        public UdpReceiver(int localPort = 8889)
        {
            _port = localPort;
        }

        // Bắt đầu lắng nghe UDP trên background thread
        public void Start()
        {
            if (_listenTask != null && !_listenTask.IsCompleted)
            {
                Console.WriteLine("[UdpReceiver] Đã đang chạy.");
                return;
            }

            _udpClient = new UdpClient(_port);
            _cts = new CancellationTokenSource();

            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
            Console.WriteLine($"[UdpReceiver] Đang lắng nghe UDP port {_port}...");
        }

        // Dừng lắng nghe
        public void Stop()
        {
            _cts?.Cancel();
            _udpClient?.Close();
            Console.WriteLine("[UdpReceiver] Đã dừng.");
        }

        // ── Vòng lặp nhận gói UDP ──
        private void ListenLoop(CancellationToken token)
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Blocking receive — unblock khi _udpClient.Close() được gọi
                    byte[] data = _udpClient.Receive(ref remoteEP);
                    ProcessPacket(data);
                }
                catch (SocketException)
                {
                    // Xảy ra khi _udpClient bị Close() — thoát vòng lặp bình thường
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UdpReceiver] Lỗi nhận: {ex.Message}");
                }
            }
        }

        // ── Xử lý từng gói nhận được ──
        private void ProcessPacket(byte[] data)
        {
            try
            {
                Packet packet = Packet.Deserialize(data);
                string json = Encoding.UTF8.GetString(packet.Payload);
                DrawPayload payload = JsonConvert.DeserializeObject<DrawPayload>(json);

                // Phân loại theo CommandType và raise đúng event
                switch (packet.Cmd)
                {
                    case CommandType.DRAW:
                        NetworkEvents.RaiseDrawReceived(payload);
                        break;

                    case CommandType.FLOOD_FILL:
                        NetworkEvents.RaiseFloodFillReceived(payload);
                        break;

                    case CommandType.TEXT:
                        NetworkEvents.RaiseTextReceived(payload);
                        break;

                    case CommandType.LASER:
                        NetworkEvents.RaiseLaserReceived(payload);
                        break;

                    case CommandType.REACTION:
                        NetworkEvents.RaiseReactionReceived(payload);
                        break;

                    default:
                        Console.WriteLine($"[UdpReceiver] CMD không xử lý qua UDP: {packet.Cmd}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UdpReceiver] Lỗi xử lý packet: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _udpClient?.Dispose();
                _cts?.Dispose();
                _disposed = true;
            }
        }
    }
}