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
        public volatile bool IsDisconnected;

        // XU LY DA LUONG: chi cho 1 task ghi vao SslStream tai mot thoi diem.
        // Neu khong khoa, nhieu broadcast cung luc co the lam vo packet TCP.
        public SemaphoreSlim WriteLock { get; } = new SemaphoreSlim(1, 1);

        public ClientSession(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
        }
    }
}
