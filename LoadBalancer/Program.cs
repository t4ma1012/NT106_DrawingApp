// ============================================================
// LoadBalancer/Program.cs
// Entry point cho Load Balancer
// Kiến trúc:
//   Client → LoadBalancer(:9000) → DrawingServer1(:8888) hoặc
//                                  DrawingServer2(:8890)
//
// Cách chạy:
//   1. Chạy DrawingServer trên port 8888 (Server1)
//   2. Chạy DrawingServer thứ 2 trên port 8890 (Server2) [tuỳ chọn]
//   3. Chạy LoadBalancer này
//   4. Client kết nối vào port 9000
// ============================================================
using System;
using System.Threading.Tasks;

namespace LoadBalancer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "NT106 Load Balancer";

            PrintBanner();

            var lb = new DrawingLoadBalancer();

            // Server1: port 8888 (DrawingServer chính)
            lb.AddServer("127.0.0.1", 8888, "DrawingServer-1");

            // Server2: port 8890 (instance thứ 2, tuỳ chọn)
            lb.AddServer("127.0.0.1", 8890, "DrawingServer-2");

            Console.WriteLine("[LB] Nhấn Ctrl+C để dừng.\n");

            // LoadBalancer lắng nghe port 9000
            // Client kết nối vào 9000 thay vì trực tiếp 8888
            await lb.StartAsync(listenPort: 9000);
        }

        static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════╗");
            Console.WriteLine("║         NT106 Drawing App                    ║");
            Console.WriteLine("║      Load Balancer — Least Connection        ║");
            Console.WriteLine("╠══════════════════════════════════════════════╣");
            Console.WriteLine("║  Port nghe : 9000                            ║");
            Console.WriteLine("║  Server 1  : 127.0.0.1:8888                 ║");
            Console.WriteLine("║  Server 2  : 127.0.0.1:8890 (tuy chon)     ║");
            Console.WriteLine("║  Health check : moi 5 giay                  ║");
            Console.WriteLine("╚══════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
