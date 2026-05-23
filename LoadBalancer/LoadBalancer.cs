using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LoadBalancer
{
    public class ServerInfo
    {
        public string ServerId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int TcpPort { get; set; }
        public int UdpPort { get; set; }
        public int ActiveProxyConnections { get; set; }
        public int RoutedClients { get; set; }
        public bool IsHealthy { get; set; } = true;
        public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;
    }

    public class DrawingLoadBalancer
    {
        private readonly List<ServerInfo> _servers = new List<ServerInfo>();
        private readonly object _lock = new object();
        private TcpListener _listener;

        public void AddServer(string host, int tcpPort, int udpPort, string name, string serverId = "")
        {
            if (string.IsNullOrWhiteSpace(serverId))
                serverId = name;

            var server = new ServerInfo
            {
                Host = host,
                TcpPort = tcpPort,
                UdpPort = udpPort,
                Name = name,
                ServerId = serverId
            };

            lock (_lock)
            {
                _servers.Add(server);
            }

            Console.WriteLine($"[LB] Added {name} {host}:{tcpPort}/{udpPort}");
        }

        public async Task StartAsync(int listenPort)
        {
            _ = Task.Run(HealthCheckLoop);

            _listener = new TcpListener(IPAddress.Any, listenPort);
            _listener.Start();
            Console.WriteLine($"[LB] Listening on {listenPort}");

            while (true)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient clientConn)
        {
            string clientIp = ((IPEndPoint)clientConn.Client.RemoteEndPoint).Address.ToString();
            NetworkStream clientStream = clientConn.GetStream();

            byte[] probe = new byte[8];
            int read = 0;
            try
            {
                read = await clientStream.ReadAsync(probe, 0, probe.Length);
                if (read <= 0)
                {
                    clientConn.Close();
                    return;
                }
            }
            catch
            {
                clientConn.Close();
                return;
            }

            string probeText = Encoding.ASCII.GetString(probe, 0, read);
            if (probeText.StartsWith("ROUTE", StringComparison.OrdinalIgnoreCase))
            {
                ServerInfo routeTarget = SelectServer();
                if (routeTarget == null)
                {
                    await SendRouteErrorAsync(clientStream, "no_healthy_server");
                    clientConn.Close();
                    return;
                }

                lock (_lock)
                {
                    routeTarget.RoutedClients++;
                }

                await SendRouteResponseAsync(clientStream, routeTarget);
                Console.WriteLine($"[LB] ROUTE {clientIp} -> {routeTarget.Name} ({routeTarget.Host}:{routeTarget.TcpPort}/{routeTarget.UdpPort})");
                clientConn.Close();
                return;
            }

            ServerInfo target = SelectServer();
            if (target == null)
            {
                clientConn.Close();
                return;
            }

            try
            {
                using var serverConn = new TcpClient();
                await serverConn.ConnectAsync(target.Host, target.TcpPort);

                lock (_lock)
                {
                    target.ActiveProxyConnections++;
                }

                NetworkStream serverStream = serverConn.GetStream();
                await serverStream.WriteAsync(probe, 0, read);
                await serverStream.FlushAsync();

                Console.WriteLine($"[LB] PROXY {clientIp} -> {target.Name} ({target.ActiveProxyConnections} active)");

                var t1 = ForwardAsync(clientStream, serverStream);
                var t2 = ForwardAsync(serverStream, clientStream);
                await Task.WhenAny(t1, t2);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LB] Proxy error: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    target.ActiveProxyConnections = Math.Max(0, target.ActiveProxyConnections - 1);
                }
                clientConn.Close();
            }
        }

        private ServerInfo SelectServer()
        {
            lock (_lock)
            {
                ServerInfo best = null;
                foreach (var s in _servers)
                {
                    if (!s.IsHealthy)
                        continue;

                    if (best == null)
                    {
                        best = s;
                        continue;
                    }

                    int bestLoad = best.ActiveProxyConnections + best.RoutedClients;
                    int curLoad = s.ActiveProxyConnections + s.RoutedClients;
                    if (curLoad < bestLoad)
                        best = s;
                }
                return best;
            }
        }

        private static async Task SendRouteResponseAsync(NetworkStream stream, ServerInfo server)
        {
            string json = JsonConvert.SerializeObject(new
            {
                host = server.Host,
                tcpPort = server.TcpPort,
                udpPort = server.UdpPort,
                serverId = server.ServerId,
                serverName = server.Name
            });
            byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        }

        private static async Task SendRouteErrorAsync(NetworkStream stream, string error)
        {
            string json = JsonConvert.SerializeObject(new { error });
            byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        }

        private async Task ForwardAsync(NetworkStream from, NetworkStream to)
        {
            byte[] buf = new byte[8192];
            while (true)
            {
                int read;
                try
                {
                    read = await from.ReadAsync(buf, 0, buf.Length);
                }
                catch
                {
                    break;
                }

                if (read <= 0)
                    break;

                try
                {
                    await to.WriteAsync(buf, 0, read);
                    await to.FlushAsync();
                }
                catch
                {
                    break;
                }
            }
        }

        private async Task HealthCheckLoop()
        {
            while (true)
            {
                await Task.Delay(5000);

                List<ServerInfo> snapshot;
                lock (_lock)
                {
                    snapshot = new List<ServerInfo>(_servers);
                }

                foreach (var server in snapshot)
                {
                    bool healthy = await PingAsync(server.Host, server.TcpPort);
                    bool changed;

                    lock (_lock)
                    {
                        changed = server.IsHealthy != healthy;
                        server.IsHealthy = healthy;
                        server.LastHealthCheck = DateTime.UtcNow;
                    }

                    if (changed)
                    {
                        Console.WriteLine(healthy
                            ? $"[LB] {server.Name} ONLINE"
                            : $"[LB] {server.Name} OFFLINE");
                    }
                }
            }
        }

        private static async Task<bool> PingAsync(string host, int port)
        {
            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync(host, port);
                return await Task.WhenAny(connectTask, Task.Delay(1500)) == connectTask && tcp.Connected;
            }
            catch
            {
                return false;
            }
        }
    }
}
