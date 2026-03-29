// ============================================================
// DrawingClient/Network/SecureTcpClient.cs
// Tuần 4 — Bọc TcpClient bằng SslStream để bảo vệ TCP
// Đặc biệt quan trọng cho CMD_LOGIN (bảo vệ password)
// ============================================================
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SharedLib.Packets;

namespace DrawingClient.Network
{
    /// <summary>
    /// TcpClient với SslStream — tất cả dữ liệu TCP được mã hóa TLS.
    /// Server cần có self-signed cert (xem SecureTcpServer.cs).
    /// </summary>
    public class SecureTcpClient : IDisposable
    {
        private TcpClient _tcpClient;
        private SslStream _sslStream;
        private bool _disposed = false;

        public bool IsConnected => _tcpClient?.Connected ?? false;

        /// <summary>
        /// Kết nối tới server và thực hiện TLS handshake.
        /// Chấp nhận self-signed cert (demo purposes).
        /// </summary>
        public async Task<bool> ConnectAsync(string serverIp, int port, string serverName = "DrawingServer")
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(serverIp, port);

                _sslStream = new SslStream(
                    _tcpClient.GetStream(),
                    false,
                    ValidateServerCertificate,  // Chấp nhận self-signed
                    null
                );

                await _sslStream.AuthenticateAsClientAsync(serverName, null,
                    SslProtocols.Tls12 | SslProtocols.Tls13, false);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecureTcpClient] Lỗi kết nối SSL: {ex.Message}");
                return false;
            }
        }

        /// <summary>Gửi Packet qua SslStream (an toàn).</summary>
        public async Task SendAsync(Packet packet)
        {
            if (_sslStream == null || !IsConnected)
                throw new InvalidOperationException("Chưa kết nối SSL.");

            byte[] data = packet.Serialize();
            // Gửi độ dài trước (4 bytes big-endian), sau đó payload
            byte[] lenBytes = BitConverter.GetBytes(data.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
            await _sslStream.WriteAsync(lenBytes, 0, 4);
            await _sslStream.WriteAsync(data, 0, data.Length);
            await _sslStream.FlushAsync();
        }

        /// <summary>Đọc một Packet từ SslStream (an toàn).</summary>
        public async Task<Packet> ReceiveAsync()
        {
            if (_sslStream == null)
                throw new InvalidOperationException("Chưa kết nối SSL.");

            // Đọc 4 bytes độ dài
            byte[] lenBuf = new byte[4];
            await ReadExactAsync(_sslStream, lenBuf, 4);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBuf);
            int packetLen = BitConverter.ToInt32(lenBuf, 0);

            // Đọc đúng số bytes
            byte[] packetBuf = new byte[packetLen];
            await ReadExactAsync(_sslStream, packetBuf, packetLen);
            return Packet.Deserialize(packetBuf);
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, totalRead, count - totalRead);
                if (read == 0) throw new IOException("Kết nối bị đóng.");
                totalRead += read;
            }
        }

        /// <summary>
        /// Chấp nhận self-signed certificate cho mục đích demo.
        /// PRODUCTION: nên dùng cert được ký bởi CA tin cậy.
        /// </summary>
        private static bool ValidateServerCertificate(object sender, X509Certificate cert,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // Chấp nhận tất cả cert trong môi trường demo
            // TODO production: kiểm tra sslPolicyErrors == None
            return true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _sslStream?.Dispose();
                _tcpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}
