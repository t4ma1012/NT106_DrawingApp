using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using SharedLib.Packets; // Sử dụng PacketDef của Người B

namespace DrawingServer
{
    public class UdpServer
    {
        // Khai báo = null! để fix warning CS8618
        private UdpClient _udpListener = null!;

        public async Task StartAsync()
        {
            _udpListener = new UdpClient(8889);
            Console.WriteLine("UDP Server dang chay tren port 8889 (Realtime Broadcast)...");

            while (true)
            {
                try
                {
                    UdpReceiveResult result = await _udpListener.ReceiveAsync();
                    byte[] receivedBytes = result.Buffer;
                    IPEndPoint senderEndPoint = result.RemoteEndPoint;

                    // Dùng code của B để mở gói tin kiểm tra lệnh
                    Packet packet = Packet.Deserialize(receivedBytes);

                    // Chỉ phát sóng nếu là lệnh vẽ/tương tác của Tuần 2
                    if (packet.Cmd == CommandType.DRAW ||
                        packet.Cmd == CommandType.CURSOR ||
                        packet.Cmd == CommandType.LASER ||
                        packet.Cmd == CommandType.FLOOD_FILL ||
                        packet.Cmd == CommandType.TEXT ||
                        packet.Cmd == CommandType.REACTION)
                    {
                        // Gửi thẳng mảng byte gốc để tăng tốc độ server
                        await BroadcastAsync(receivedBytes, senderEndPoint);
                    }
                }
                catch (Exception)
                {
                    // Bỏ qua im lặng các gói tin rác hoặc không parse được
                }
            }
        }

        private async Task BroadcastAsync(byte[] data, IPEndPoint senderEndPoint)
        {
            foreach (var kvp in Server.Clients)
            {
                var session = kvp.Value;

                // Tuần 2: Bỏ qua nếu người gửi đang ở chế độ Chỉ Xem (Spectator)
                if (session.IsSpectator && session.UdpEndPoint != null && session.UdpEndPoint.Equals(senderEndPoint))
                {
                    continue;
                }

                // Gửi tới tất cả các client KHÁC
                if (session.UdpEndPoint != null && !session.UdpEndPoint.Equals(senderEndPoint))
                {
                    await _udpListener.SendAsync(data, data.Length, session.UdpEndPoint);
                }
            }
        }
    }
}