// ============================================================
// DrawingClient/Network/SecureUdpSender.cs
// Tuần 4 — UdpSender nâng cấp: mã hóa AES-256 trước khi gửi
// ============================================================
using System;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using SharedLib.Packets;
using SharedLib.Payloads;
using SharedLib.Security;
using SharedLib.Logging;

namespace DrawingClient.Network
{
    /// <summary>
    /// Gửi UDP packet đã mã hóa AES-256.
    /// Format: [IV(16B)] + [AES_Encrypted(Packet.Serialize())]
    /// </summary>
    public class SecureUdpSender
    {
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _serverEndPoint;

        // Event để UI xử lý khi gửi thất bại
        public event Action<CommandType, string> OnSendError;

        public SecureUdpSender(string serverIp, int udpPort = 8889)
        {
            _udpClient = new UdpClient();
            _serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), udpPort);
        }

        /// <summary>Mã hóa và gửi DrawPayload qua UDP.</summary>
        public void SendDraw(DrawPayload payload)
            => SendEncrypted(CommandType.DRAW, payload);

        public void SendFloodFill(FloodFillPayload payload)
            => SendEncrypted(CommandType.FLOOD_FILL, payload);

        public void SendCursor(CursorPayload payload)
            => SendEncrypted(CommandType.CURSOR, payload);

        public void SendLaser(LaserPayload payload)
            => SendEncrypted(CommandType.LASER, payload);

        public void SendReaction(ReactionPayload payload)
            => SendEncrypted(CommandType.REACTION, payload);

        public void SendSpotlight(SpotlightPayload payload)
            => SendEncrypted(CommandType.SPOTLIGHT, payload);

        public void SendPixelArt(PixelArtDrawPayload payload)
            => SendEncrypted(CommandType.PIXEL_ART_DRAW, payload);

        private void SendEncrypted(CommandType cmd, object payload)
        {
            try
            {
                var packet = PacketHelper.Create(cmd, payload);
                byte[] raw = packet.Serialize();
                byte[] encrypted = AesHelper.Encrypt(raw);
                _udpClient.Send(encrypted, encrypted.Length, _serverEndPoint);
            }
            catch (SocketException sockEx)
            {
                string errMsg = $"Socket lỗi ({sockEx.SocketErrorCode}): {sockEx.Message}";
                Logger.Error("SecureUdpSender", errMsg);
                OnSendError?.Invoke(cmd, errMsg);
            }
            catch (ArgumentException argEx)
            {
                string errMsg = $"Invalid payload {cmd}: {argEx.Message}";
                Logger.Error("SecureUdpSender", errMsg);
                OnSendError?.Invoke(cmd, errMsg);
            }
            catch (Exception ex)
            {
                string errMsg = $"Lỗi gửi {cmd}: {ex.GetType().Name} - {ex.Message}";
                Logger.Exception("SecureUdpSender", ex);
                OnSendError?.Invoke(cmd, errMsg);
            }
        }

        public void Close() => _udpClient?.Close();
    }
}
