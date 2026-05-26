using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using SharedLib.Config;
using SharedLib.Packets;
using SharedLib.Security;

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
        private readonly ConcurrentDictionary<string, string> _roomOwnerCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UdpProxySession> _udpSessions = new Dictionary<string, UdpProxySession>();
        private readonly object _lock = new object();
        private TcpListener _listener;
        private UdpClient _udpListener;
        public string RoutingStrategy { get; set; } = "room-affinity";
        public string DatabaseUrl { get; set; } = "";

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

        public async Task StartAsync(int listenPort, int udpPort)
        {
            _ = Task.Run(HealthCheckLoop);
            _ = Task.Run(() => StartUdpProxyAsync(udpPort));

            _listener = new TcpListener(IPAddress.Any, listenPort);
            _listener.Start();
            Console.WriteLine($"[LB] TCP listening on {listenPort}");

            while (true)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task StartUdpProxyAsync(int udpPort)
        {
            _udpListener = new UdpClient(udpPort);
            Console.WriteLine($"[LB] UDP proxy listening on {udpPort}");

            while (true)
            {
                try
                {
                    UdpReceiveResult result = await _udpListener.ReceiveAsync();
                    _ = Task.Run(() => HandleUdpFromClientAsync(result));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LB][UDP] receive error: {ex.Message}");
                }
            }
        }

        private async Task HandleUdpFromClientAsync(UdpReceiveResult result)
        {
            string key = result.RemoteEndPoint.ToString();
            UdpProxySession session;

            lock (_lock)
            {
                _udpSessions.TryGetValue(key, out session);
            }

            if (session == null)
            {
                string serverId = TryExtractUdpTargetServerId(result.Buffer);
                ServerInfo target = SelectServerById(serverId) ?? SelectServer();
                if (target == null)
                    return;

                session = new UdpProxySession(result.RemoteEndPoint, target, this);
                lock (_lock)
                {
                    _udpSessions[key] = session;
                }

                session.StartReceiveLoop();
                Console.WriteLine($"[LB][UDP] {result.RemoteEndPoint} -> {target.Name} ({target.Host}:{target.UdpPort})");
            }

            session.LastSeenUtc = DateTime.UtcNow;
            await session.SendToServerAsync(result.Buffer);
        }

        internal async Task SendUdpToClientAsync(byte[] data, IPEndPoint clientEndPoint)
        {
            var listener = _udpListener;
            if (listener == null)
                return;

            try
            {
                await listener.SendAsync(data, data.Length, clientEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LB][UDP] send to client {clientEndPoint} failed: {ex.Message}");
            }
        }

        private static string TryExtractUdpTargetServerId(byte[] encryptedPacket)
        {
            try
            {
                byte[] decrypted = AesHelper.Decrypt(encryptedPacket);
                Packet packet = Packet.Deserialize(decrypted);
                if (packet.Payload == null || packet.Payload.Length == 0)
                    return "";

                string json = Encoding.UTF8.GetString(packet.Payload);
                var obj = JObject.Parse(json);
                return obj["ServerId"]?.ToString() ?? obj["serverId"]?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private async Task HandleClientAsync(TcpClient clientConn)
        {
            clientConn.NoDelay = true;
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
                string routeLine = await ReadAsciiLineAsync(clientStream, probeText, 256);
                string roomCode = ExtractRouteRoomCode(routeLine);
                ServerInfo routeTarget = await SelectServerForRouteAsync(roomCode);
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

            bool hasRelayPreface = probeText.StartsWith("RELAY", StringComparison.OrdinalIgnoreCase);
            string relayLine = hasRelayPreface
                ? await ReadAsciiLineAsync(clientStream, probeText, 256)
                : "";

            ServerInfo target = hasRelayPreface
                ? SelectServerById(ExtractRelayServerId(relayLine)) ?? SelectServer()
                : SelectServer();
            if (target == null)
            {
                clientConn.Close();
                return;
            }

            try
            {
                using var serverConn = new TcpClient();
                serverConn.NoDelay = true;
                await serverConn.ConnectAsync(target.Host, target.TcpPort);

                lock (_lock)
                {
                    target.ActiveProxyConnections++;
                }

                NetworkStream serverStream = serverConn.GetStream();
                if (!hasRelayPreface)
                {
                    await serverStream.WriteAsync(probe, 0, read);
                    await serverStream.FlushAsync();
                }

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
                if (string.Equals(RoutingStrategy, "primary", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var s in _servers)
                    {
                        if (s.IsHealthy)
                            return s;
                    }

                    return null;
                }

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

        private ServerInfo SelectServerById(string serverId)
        {
            if (string.IsNullOrWhiteSpace(serverId))
                return null;

            lock (_lock)
            {
                foreach (var s in _servers)
                {
                    if (s.IsHealthy && string.Equals(s.ServerId, serverId, StringComparison.OrdinalIgnoreCase))
                        return s;
                }
            }

            return null;
        }

        private async Task<ServerInfo> SelectServerForRouteAsync(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
                return SelectServer();

            if (_roomOwnerCache.TryGetValue(roomCode.Trim(), out string cachedOwnerId))
            {
                ServerInfo cachedOwner = SelectServerById(cachedOwnerId);
                if (cachedOwner != null)
                    return cachedOwner;
            }

            string ownerServerId = await GetRoomOwnerServerIdAsync(roomCode);
            ServerInfo owner = SelectServerById(ownerServerId);
            if (owner != null)
            {
                _roomOwnerCache[roomCode.Trim()] = owner.ServerId;
                return owner;
            }

            Console.WriteLine($"[LB] ROUTE room={roomCode} blocked: owner server is unknown or unhealthy");
            return null;
        }

        private async Task<string> GetRoomOwnerServerIdAsync(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(DatabaseUrl))
                return "";

            try
            {
                using var conn = new NpgsqlConnection(PostgresConnectionString.Normalize(DatabaseUrl));
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(
                    "SELECT owner_server_id FROM Rooms WHERE room_code = @room_code LIMIT 1",
                    conn);
                cmd.Parameters.AddWithValue("room_code", roomCode.Trim());
                object result = await cmd.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? "" : Convert.ToString(result) ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LB] Room owner lookup failed for {roomCode}: {ex.Message}");
                return "";
            }
        }

        private static async Task<string> ReadAsciiLineAsync(NetworkStream stream, string initialText, int maxChars)
        {
            var sb = new StringBuilder(initialText ?? "");
            byte[] one = new byte[1];

            while (sb.Length < maxChars && sb.ToString().IndexOf('\n') < 0)
            {
                int read = await stream.ReadAsync(one, 0, 1);
                if (read <= 0)
                    break;
                sb.Append((char)one[0]);
            }

            return sb.ToString().Trim();
        }

        private static string ExtractRouteRoomCode(string routeLine)
        {
            if (string.IsNullOrWhiteSpace(routeLine))
                return "";

            string[] parts = routeLine.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return "";

            string value = parts[1].Trim();
            const string roomPrefix = "room=";
            if (value.StartsWith(roomPrefix, StringComparison.OrdinalIgnoreCase))
                value = value.Substring(roomPrefix.Length);
            return value;
        }

        private static string ExtractRelayServerId(string relayLine)
        {
            if (string.IsNullOrWhiteSpace(relayLine))
                return "";

            string[] parts = relayLine.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return "";

            string value = parts[1].Trim();
            const string idPrefix = "server=";
            if (value.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase))
                value = value.Substring(idPrefix.Length);
            return value;
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
                if (await Task.WhenAny(connectTask, Task.Delay(1500)) != connectTask || !tcp.Connected)
                    return false;

                using var ssl = new SslStream(tcp.GetStream(), false, (sender, cert, chain, errors) => true);
                var authTask = ssl.AuthenticateAsClientAsync("DrawingServer", null, SslProtocols.Tls12, false);
                return await Task.WhenAny(authTask, Task.Delay(1500)) == authTask && ssl.IsAuthenticated;
            }
            catch
            {
                return false;
            }
        }

        private sealed class UdpProxySession
        {
            private readonly UdpClient _serverSocket;
            private readonly IPEndPoint _serverEndPoint;
            private readonly IPEndPoint _clientEndPoint;
            private readonly DrawingLoadBalancer _owner;

            public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

            public UdpProxySession(IPEndPoint clientEndPoint, ServerInfo server, DrawingLoadBalancer owner)
            {
                _clientEndPoint = clientEndPoint;
                _serverEndPoint = new IPEndPoint(Dns.GetHostAddresses(server.Host)[0], server.UdpPort);
                _owner = owner;
                _serverSocket = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            }

            public void StartReceiveLoop()
            {
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            UdpReceiveResult response = await _serverSocket.ReceiveAsync();
                            await _owner.SendUdpToClientAsync(response.Buffer, _clientEndPoint);
                        }
                        catch
                        {
                            break;
                        }
                    }
                });
            }

            public Task SendToServerAsync(byte[] data)
            {
                return _serverSocket.SendAsync(data, data.Length, _serverEndPoint);
            }
        }
    }
}
