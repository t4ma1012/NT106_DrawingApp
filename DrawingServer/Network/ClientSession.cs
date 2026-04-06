#nullable enable
using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace DrawingServer.Network
{
    public class ClientSession
    {
        public TcpClient TcpClient { get; set; }
        public IPEndPoint? UdpEndPoint { get; set; } // Dùng để gửi dữ vẽ realtime qua UDP
        public string? Username { get; set; }
        public string RoomCode { get; set; } = "";    // Lưu mã phòng người này đang ở
        public string AssignedColor { get; set; } = "#000000";

        public ClientSession(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
        }
        public SslStream? SecureStream { get; set; } // Thêm đường ống bảo mật
    }
}