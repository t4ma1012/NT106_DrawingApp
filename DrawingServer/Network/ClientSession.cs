#nullable enable
// ============================================================
// DrawingServer/Network/ClientSession.cs — FIX
// Thêm SemaphoreSlim để tránh race condition khi nhiều task
// cùng ghi vào SslStream một lúc (gây vỡ packet TCP)
// ============================================================
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;

namespace DrawingServer.Network
{
    public class ClientSession
    {
        public TcpClient TcpClient { get; set; }
        public IPEndPoint? UdpEndPoint { get; set; }
        public string? Username { get; set; }
        public string RoomCode { get; set; } = "";
        public string AssignedColor { get; set; } = "#000000";
        public SslStream? SecureStream { get; set; }

        // ✅ Lock để đảm bảo chỉ 1 task ghi vào stream tại một thời điểm
        // Tránh race condition khi broadcast nhiều lệnh cùng lúc
        public SemaphoreSlim WriteLock { get; } = new SemaphoreSlim(1, 1);

        public ClientSession(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
        }
    }
}
