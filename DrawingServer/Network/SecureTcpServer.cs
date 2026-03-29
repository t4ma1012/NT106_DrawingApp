// ============================================================
// DrawingServer/Network/SecureTcpServer.cs
// Tuần 4 — Server-side SslStream wrapper
// Tạo self-signed cert và bọc mọi TCP connection bằng TLS
// ============================================================
using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SharedLib.Packets;

namespace DrawingServer.Network
{
    /// <summary>
    /// Server SSL helper — tạo self-signed cert và xác thực TLS cho mỗi client.
    /// </summary>
    public static class SecureTcpServer
    {
        private static X509Certificate2 _serverCert;

        /// <summary>
        /// Tạo (hoặc load) self-signed certificate cho server.
        /// Gọi một lần khi server khởi động.
        /// </summary>
        public static X509Certificate2 GetOrCreateCertificate(string certPath = "server.pfx",
            string certPassword = "NT106_DrawingApp")
        {
            if (_serverCert != null) return _serverCert;

            // Thử load từ file
            if (System.IO.File.Exists(certPath))
            {
                _serverCert = new X509Certificate2(certPath, certPassword);
                Console.WriteLine("[SSL] Đã load certificate từ file.");
                return _serverCert;
            }

            // Tạo self-signed cert mới
            using var rsa = RSA.Create(2048);
            var certRequest = new CertificateRequest(
                "CN=DrawingServer_NT106",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            certRequest.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));
            certRequest.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));

            var cert = certRequest.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(5));

            _serverCert = new X509Certificate2(cert.Export(X509ContentType.Pfx, certPassword), certPassword);

            // Lưu cert để dùng lại lần sau
            System.IO.File.WriteAllBytes(certPath, _serverCert.Export(X509ContentType.Pfx, certPassword));
            Console.WriteLine("[SSL] Đã tạo self-signed certificate mới.");
            return _serverCert;
        }

        /// <summary>
        /// Bọc TcpClient bằng SslStream (server-side authentication).
        /// Trả về SslStream đã sẵn sàng đọc/ghi.
        /// </summary>
        public static async Task<SslStream> WrapClientAsync(TcpClient client)
        {
            var cert = GetOrCreateCertificate();
            var sslStream = new SslStream(client.GetStream(), false);
            await sslStream.AuthenticateAsServerAsync(
                cert,
                clientCertificateRequired: false,
                enabledSslProtocols: SslProtocols.Tls12 | SslProtocols.Tls13,
                checkCertificateRevocation: false);
            return sslStream;
        }
    }
}
