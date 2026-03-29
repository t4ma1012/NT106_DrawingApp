// ============================================================
// DrawingClient/Network/SecureUdpReceiver.cs
// Tuần 4 — UdpReceiver nâng cấp: giải mã AES-256 khi nhận
// ============================================================
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SharedLib.Packets;
using SharedLib.Payloads;
using SharedLib.Security;

namespace DrawingClient.Network
{
    /// <summary>
    /// Lắng nghe UDP port 8889, giải mã AES-256, raise NetworkEvents.
    /// </summary>
    public class SecureUdpReceiver
    {
        private UdpClient _udpClient;
        private Thread _receiveThread;
        private readonly int _port;
        private volatile bool _running = false;

        public SecureUdpReceiver(int listenPort = 8889)
        {
            _port = listenPort;
        }

        public void Start()
        {
            _udpClient = new UdpClient(_port);
            _running = true;
            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "SecureUDP-Recv" };
            _receiveThread.Start();
            Console.WriteLine($"[SecureUdpReceiver] Lắng nghe UDP cổng {_port} (AES-256)");
        }

        public void Stop()
        {
            _running = false;
            _udpClient?.Close();
        }

        private void ReceiveLoop()
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    byte[] encrypted = _udpClient.Receive(ref remote);
                    // Giải mã AES
                    byte[] raw = AesHelper.Decrypt(encrypted);
                    Packet packet = Packet.Deserialize(raw);
                    ProcessPacket(packet);
                }
                catch (SocketException) when (!_running)
                {
                    break; // Stop() được gọi
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SecureUdpReceiver] Lỗi: {ex.Message}");
                }
            }
        }

        private void ProcessPacket(Packet packet)
        {
            switch (packet.Cmd)
            {
                case CommandType.DRAW:
                    var draw = PacketHelper.GetPayload<DrawPayload>(packet);
                    NetworkEvents.RaiseDrawReceived(draw);
                    break;

                case CommandType.FLOOD_FILL:
                    var fill = PacketHelper.GetPayload<FloodFillPayload>(packet);
                    NetworkEvents.RaiseFloodFillReceived(fill);
                    break;

                case CommandType.CURSOR:
                    var cursor = PacketHelper.GetPayload<CursorPayload>(packet);
                    NetworkEvents.RaiseCursorReceived(cursor);
                    break;

                case CommandType.LASER:
                    var laser = PacketHelper.GetPayload<LaserPayload>(packet);
                    NetworkEvents.RaiseLaserReceived(laser);
                    break;

                case CommandType.REACTION:
                    var reaction = PacketHelper.GetPayload<ReactionPayload>(packet);
                    NetworkEvents.RaiseReactionReceived(reaction);
                    break;

                case CommandType.SPOTLIGHT:
                    var spotlight = PacketHelper.GetPayload<SpotlightPayload>(packet);
                    NetworkEvents.RaiseSpotlightReceived(spotlight);
                    break;

                case CommandType.PIXEL_ART_DRAW:
                    var pixel = PacketHelper.GetPayload<PixelArtDrawPayload>(packet);
                    NetworkEvents.RaisePixelArtDrawReceived(pixel);
                    break;

                default:
                    Console.WriteLine($"[SecureUdpReceiver] Unknown UDP cmd: {packet.Cmd}");
                    break;
            }
        }
    }
}
