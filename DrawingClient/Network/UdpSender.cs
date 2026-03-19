using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SharedLib.Packets;
using SharedLib.Payloads;
using Newtonsoft.Json;

namespace DrawingClient.Network
{
    // ═══════════════════════════════════════════════════════
    // GỬI DỮ LIỆU VẼ QUA UDP ĐẾN SERVER PORT 8889
    // Người A gọi các method này khi người dùng vẽ
    // ═══════════════════════════════════════════════════════
    public class UdpSender : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _serverEndPoint;
        private bool _disposed = false;

        // serverIP: IP của máy chạy server, vd "192.168.1.100" hoặc "127.0.0.1"
        public UdpSender(string serverIP, int port = 8889)
        {
            _udpClient = new UdpClient();
            _serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), port);
        }

        // ── Gửi nét vẽ thông thường (CMD_DRAW) ──
        public void SendDraw(DrawPayload payload)
        {
            payload.ToolType = payload.ToolType ?? "pen";
            Send(CommandType.DRAW, payload);
        }

        // ── Gửi flood fill (CMD_FLOOD_FILL) ──
        // Người A gọi khi bấm flood fill tại điểm (x, y) với màu colorARGB
        public void SendFloodFill(string username, int x, int y, int colorARGB)
        {
            var payload = new DrawPayload
            {
                Username = username,
                ToolType = "floodfill",
                X1 = x,
                Y1 = y,
                ColorARGB = colorARGB
            };
            Send(CommandType.FLOOD_FILL, payload);
        }

        // ── Gửi text tool (CMD_TEXT) ──
        // Người A gọi sau khi người dùng nhập xong text và xác nhận
        public void SendText(string username, int x, int y, string text,
                             string fontName, int fontSize, int colorARGB)
        {
            var payload = new DrawPayload
            {
                Username = username,
                ToolType = "text",
                X1 = x,
                Y1 = y,
                Text = text,
                FontName = fontName,
                FontSize = fontSize,
                ColorARGB = colorARGB
            };
            Send(CommandType.TEXT, payload);
        }

        // ── Gửi laser pointer (CMD_LASER) ──
        // Người A gọi liên tục khi giữ Alt + di chuột
        public void SendLaser(string username, int x, int y)
        {
            var payload = new DrawPayload
            {
                Username = username,
                ToolType = "laser",
                X1 = x,
                Y1 = y
            };
            Send(CommandType.LASER, payload);
        }

        // ── Gửi reaction emoji (CMD_REACTION) ──
        // Người A gọi khi bấm phím 1/2/3
        // emojiCode: "1"=👍, "2"=❤️, "3"=😂
        public void SendReaction(string username, string emojiCode, int x, int y)
        {
            var payload = new DrawPayload
            {
                Username = username,
                ToolType = "reaction",
                EmojiCode = emojiCode,
                X1 = x,
                Y1 = y
            };
            Send(CommandType.REACTION, payload);
        }

        // ── Gửi cursor position (CMD_CURSOR) ──
        // Người A gọi liên tục khi MouseMove
        public void SendCursor(string username, int x, int y)
        {
            var payload = new DrawPayload
            {
                Username = username,
                ToolType = "cursor",
                X1 = x,
                Y1 = y
            };
            Send(CommandType.CURSOR, payload);
        }

        // ── Internal: serialize payload → Packet → byte[] → gửi UDP ──
        private void Send(CommandType cmd, DrawPayload payload)
        {
            try
            {
                string json = JsonConvert.SerializeObject(payload);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                var packet = new Packet
                {
                    Cmd = cmd,
                    Payload = jsonBytes
                };

                byte[] data = packet.Serialize();
                _udpClient.Send(data, data.Length, _serverEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UdpSender] Lỗi gửi {cmd}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _udpClient?.Close();
                _disposed = true;
            }
        }
    }
}