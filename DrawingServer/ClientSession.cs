using System.Net;
using System.Net.Sockets;

namespace DrawingServer
{
    public class ClientSession
    {
        public string? Username { get; set; }
        public TcpClient TcpClient { get; set; }
        public IPEndPoint? UdpEndPoint { get; set; }
        public string? AssignedColor { get; set; }
        public bool IsSpectator { get; set; }

        public ClientSession(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
        }
    }
}