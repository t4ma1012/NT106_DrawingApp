using System;
using System.Threading.Tasks;
using DrawingServer.Network;
using SharedLib.Logging;

namespace DrawingServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Bật Logging
            Logger.Initialize("server_logs.txt");
            Console.WriteLine("Khởi động Drawing Server...");

            // KHÚC NÀY QUAN TRỌNG: Server cần 1 file chứng chỉ server.pfx để chạy TLS
            // Tạm thời mình để file tên là "server.pfx" và mật khẩu "123456"
            // (Lát nữa mình sẽ chỉ bạn cách tạo file này bằng 1 dòng lệnh)
            string pfxPath = "server.pfx";
            string pfxPassword = "123456";

            SecureTcpServer tcpServer = new SecureTcpServer();
            SecureUdpServer udpServer = new SecureUdpServer();

            // Chạy UDP ngầm
            _ = Task.Run(() => udpServer.StartAsync());

            // Chạy TCP chính
            await tcpServer.StartAsync(pfxPath, pfxPassword);
        }
    }
}