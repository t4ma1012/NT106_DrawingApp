// ============================================================
// LoadBalancer/LoadBalancer.cs
// Tuần 4 — Load Balancer: least-connection routing
// Console riêng, lắng nghe port 8888
// Điều hướng tới DrawingServer (port 8001, 8002)
// Health check ping mỗi 5 giây
// ============================================================
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LoadBalancer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "NT106 Load Balancer";
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║     NT106 Load Balancer v1.0         ║");
            Console.WriteLine("║   Least-Connection Algorithm          ║");
            Console.WriteLine("╚══════════════════════════════════════╝");

            var lb = new DrawingLoadBalancer();
            lb.AddServer("127.0.0.1", 8001, "Server1");
            lb.AddServer("127.0.0.1", 8002, "Server2");
            await lb.StartAsync(8888);
        }
    }

    public class ServerInfo
    {
        public string Name { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public int ActiveConnections { get; set; } = 0;
        public bool IsHealthy { get; set; } = true;
        public DateTime LastHealthCheck { get; set; } = DateTime.Now;

        public override string ToString() =>
            $"{Name} ({Host}:{Port}) — {ActiveConnections} clients — {(IsHealthy ? "✅ Online" : "❌ Offline")}";
    }

    public class DrawingLoadBalancer
    {
        private readonly List<ServerInfo> _servers = new List<ServerInfo>();
        private readonly object _lock = new object();
        private TcpListener _listener;

        public void AddServer(string host, int port, string name)
        {
            _servers.Add(new ServerInfo { Host = host, Port = port, Name = name });
            Console.WriteLine($"[LB] Đã thêm server: {name} ({host}:{port})");
        }

        public async Task StartAsync(int listenPort)
        {
            // Bắt đầu health check background
            _ = Task.Run(HealthCheckLoop);

            _listener = new TcpListener(IPAddress.Any, listenPort);
            _listener.Start();
            Console.WriteLine($"[LB] Load Balancer đang lắng nghe cổng {listenPort}");
            Console.WriteLine($"[LB] Điều hướng tới {_servers.Count} server con\n");

            while (true)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient clientConn)
        {
            ServerInfo target = SelectServer();
            if (target == null)
            {
                Console.WriteLine($"[{Ts()}] ⚠️  Không có server nào khả dụng, từ chối kết nối.");
                clientConn.Close();
                return;
            }

            string clientIp = ((IPEndPoint)clientConn.Client.RemoteEndPoint).Address.ToString();

            try
            {
                // Kết nối tới server đích
                using var serverConn = new TcpClient();
                await serverConn.ConnectAsync(target.Host, target.Port);

                lock (_lock) target.ActiveConnections++;

                Console.WriteLine($"[{Ts()}] 🔀 {clientIp} → {target.Name} " +
                    $"({target.Port}) — tổng: {target.ActiveConnections} clients");

                // Chuyển tiếp dữ liệu 2 chiều
                var t1 = ForwardAsync(clientConn.GetStream(), serverConn.GetStream());
                var t2 = ForwardAsync(serverConn.GetStream(), clientConn.GetStream());
                await Task.WhenAny(t1, t2);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{Ts()}] ❌ Lỗi proxy {clientIp}: {ex.Message}");
            }
            finally
            {
                lock (_lock) target.ActiveConnections = Math.Max(0, target.ActiveConnections - 1);
                clientConn.Close();
                Console.WriteLine($"[{Ts()}] 🔌 {clientIp} ngắt kết nối khỏi {target.Name} " +
                    $"— còn {target.ActiveConnections} clients");
            }
        }

        /// <summary>Thuật toán Least-Connection: chọn server ít client nhất còn healthy.</summary>
        private ServerInfo SelectServer()
        {
            lock (_lock)
            {
                ServerInfo best = null;
                foreach (var s in _servers)
                {
                    if (!s.IsHealthy) continue;
                    if (best == null || s.ActiveConnections < best.ActiveConnections)
                        best = s;
                }
                return best;
            }
        }

        private async Task ForwardAsync(NetworkStream from, NetworkStream to)
        {
            byte[] buf = new byte[4096];
            try
            {
                int read;
                while ((read = await from.ReadAsync(buf, 0, buf.Length)) > 0)
                    await to.WriteAsync(buf, 0, read);
            }
            catch { /* kết nối đóng */ }
        }

        /// <summary>Ping server con mỗi 5 giây, cập nhật IsHealthy.</summary>
        private async Task HealthCheckLoop()
        {
            while (true)
            {
                await Task.Delay(5000);
                foreach (var server in _servers)
                {
                    bool wasHealthy = server.IsHealthy;
                    bool nowHealthy = await PingServerAsync(server.Host, server.Port);

                    lock (_lock) server.IsHealthy = nowHealthy;
                    server.LastHealthCheck = DateTime.Now;

                    if (wasHealthy && !nowHealthy)
                        Console.WriteLine($"[{Ts()}] ⚠️  {server.Name} went OFFLINE");
                    else if (!wasHealthy && nowHealthy)
                        Console.WriteLine($"[{Ts()}] ✅ {server.Name} came back ONLINE");
                }
            }
        }

        private static async Task<bool> PingServerAsync(string host, int port)
        {
            try
            {
                using var ping = new TcpClient();
                var connectTask = ping.ConnectAsync(host, port);
                return await Task.WhenAny(connectTask, Task.Delay(2000)) == connectTask
                       && ping.Connected;
            }
            catch { return false; }
        }

        private static string Ts() => DateTime.Now.ToString("HH:mm:ss");
    }
}
