// ============================================================
// DrawingServer/Network/SecureUdpServer.cs
// Tuần 4 — UdpServer nâng cấp: nhận UDP AES-256, giải mã, broadcast lại
// ============================================================
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using SharedLib.Packets;
using SharedLib.Payloads;
using SharedLib.Security;

namespace DrawingServer.Network
{
    /// <summary>
    /// UDP Server nhận AES-encrypted packets, giải mã, broadcast tới các client khác.
    /// Cũng mã hóa AES khi broadcast ra.
    /// </summary>
    public class SecureUdpServer
    {
        private UdpClient _udpListener;
        private const int UDP_PORT = 8889;

        public async Task StartAsync()
        {
            _udpListener = new UdpClient(UDP_PORT);
            Console.WriteLine($"[SecureUdpServer] Lắng nghe UDP cổng {UDP_PORT} (AES-256)");

            while (true)
            {
                try
                {
                    var result = await _udpListener.ReceiveAsync();
                    _ = Task.Run(() => HandlePacketAsync(result.Buffer, result.RemoteEndPoint));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SecureUdpServer] Receive error: {ex.Message}");
                }
            }
        }

        private async Task HandlePacketAsync(byte[] encryptedData, IPEndPoint senderEndPoint)
        {
            try
            {
                // Giải mã AES
                byte[] raw = AesHelper.Decrypt(encryptedData);
                Packet packet = Packet.Deserialize(raw);

                // Lưu UdpEndPoint của sender vào ClientSession (nếu chưa có)
                string senderUsername = GetUsernameFromEndPoint(senderEndPoint);
                if (senderUsername != null && Server.Clients.TryGetValue(senderUsername, out var session))
                {
                    if (session.UdpEndPoint == null)
                        session.UdpEndPoint = senderEndPoint;
                }

                // Xử lý theo loại packet
                switch (packet.Cmd)
                {
                    case CommandType.DRAW:
                        var draw = PacketHelper.GetPayload<DrawPayload>(packet);
                        // TODO Người C: lưu vào DB
                        // await DbManager.SaveDrawAction(draw, currentRoomCode);
                        await BroadcastAsync(raw, senderEndPoint, draw.Username);
                        break;

                    case CommandType.FLOOD_FILL:
                        var fill = PacketHelper.GetPayload<FloodFillPayload>(packet);
                        await BroadcastAsync(raw, senderEndPoint, fill.Username);
                        break;

                    case CommandType.CURSOR:
                        // Cursor broadcast không cần lưu DB
                        var cursor = PacketHelper.GetPayload<CursorPayload>(packet);
                        await BroadcastAsync(raw, senderEndPoint, cursor.Username);
                        break;

                    case CommandType.LASER:
                        var laser = PacketHelper.GetPayload<LaserPayload>(packet);
                        await BroadcastAsync(raw, senderEndPoint, laser.Username);
                        break;

                    case CommandType.REACTION:
                        var reaction = PacketHelper.GetPayload<ReactionPayload>(packet);
                        await BroadcastAsync(raw, senderEndPoint, reaction.Username);
                        break;

                    case CommandType.SPOTLIGHT:
                        var spotlight = PacketHelper.GetPayload<SpotlightPayload>(packet);
                        await BroadcastAsync(raw, senderEndPoint, spotlight.Username);
                        break;

                    case CommandType.PIXEL_ART_DRAW:
                        var pixelArt = PacketHelper.GetPayload<PixelArtDrawPayload>(packet);
                        // TODO: rate limit X ô/phút per user
                        await BroadcastAsync(raw, senderEndPoint, pixelArt.Username);
                        break;

                    default:
                        Console.WriteLine($"[SecureUdpServer] Unknown cmd: {packet.Cmd}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecureUdpServer] HandlePacket error: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcast AES-encrypted packet tới tất cả client trong cùng phòng,
        /// trừ sender. Không gửi tới spectator.
        /// </summary>
        private async Task BroadcastAsync(byte[] rawPacket, IPEndPoint senderEndPoint,
            string senderUsername)
        {
            // Mã hóa lại để broadcast (mỗi lần broadcast có IV mới)
            byte[] encrypted = AesHelper.Encrypt(rawPacket);

            string senderRoom = GetRoomCodeForUser(senderUsername);

            foreach (var kv in Server.Clients)
            {
                var session = kv.Value;
                // Bỏ qua sender
                if (session.Username == senderUsername) continue;
                // Bỏ qua spectator
                if (session.IsSpectator) continue;
                // Chỉ gửi trong cùng phòng
                if (session.CurrentRoomCode != senderRoom) continue;
                // Cần có UdpEndPoint
                if (session.UdpEndPoint == null) continue;

                try
                {
                    await _udpListener.SendAsync(encrypted, encrypted.Length, session.UdpEndPoint);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SecureUdpServer] Broadcast error to {session.Username}: {ex.Message}");
                }
            }
        }

        private static string GetUsernameFromEndPoint(IPEndPoint ep)
        {
            foreach (var kv in Server.Clients)
                if (kv.Value.UdpEndPoint?.Equals(ep) == true)
                    return kv.Value.Username;
            return null;
        }

        private static string GetRoomCodeForUser(string username)
        {
            return Server.Clients.TryGetValue(username, out var s) ? s.CurrentRoomCode : null;
        }
    }
}
