using System;
using System.Threading.Tasks;
using DrawingServer.Network;
using DrawingServer.Services;
using SharedLib.Logging;

namespace DrawingServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Logger.Initialize("server_logs.txt");
            Console.WriteLine("Khởi động Drawing Server...");

            string pfxPath = "server.pfx";
            string pfxPassword = "123456";

            SecureTcpServer tcpServer = new SecureTcpServer();
            SecureUdpServer udpServer = new SecureUdpServer();

            // Chạy UDP ngầm
            _ = Task.Run(() => udpServer.StartAsync());

            // Khởi động Snapshot tự động mỗi 5 phút
            SnapshotService.StartAsync();

            // Chạy TCP chính
            await tcpServer.StartAsync(pfxPath, pfxPassword);
        }
    }
}
