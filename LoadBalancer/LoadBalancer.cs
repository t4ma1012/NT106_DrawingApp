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
            // XU LY DA LUONG: HealthCheckLoop va StartUdpProxyAsync duoc dua len ThreadPool bang Task.Run.
            // Trong khi 2 task nen nay dang chay, task StartAsync van tiep tuc lang nghe TCP client moi.
            _ = Task.Run(HealthCheckLoop);
            _ = Task.Run(() => StartUdpProxyAsync(udpPort));

            _listener = new TcpListener(IPAddress.Any, listenPort);
            _listener.Start();
            Console.WriteLine($"[LB] TCP listening on {listenPort}");

            while (true)
            {
                // XU LY BAT DONG BO: AcceptTcpClientAsync khong chiem thread trong luc chua co client moi.
                TcpClient client = await _listener.AcceptTcpClientAsync();
                // XU LY DA LUONG: moi ket noi proxy duoc xu ly tren task rieng.
                // Nho vay client A dang proxy/route khong lam client B phai doi.
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
                    // XU LY BAT DONG BO: ReceiveAsync cho UDP datagram khong chan cac tac vu TCP cua LoadBalancer.
                    UdpReceiveResult result = await _udpListener.ReceiveAsync();
                    // XU LY DA LUONG: moi datagram UDP proxy duoc xu ly song song tren ThreadPool.
                    // Dieu nay giup cursor/ping realtime khong bi xep hang sau cac datagram truoc do.
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

            // XU LY DA LUONG: bao ve bang session UDP vi nhieu datagram co the den dong thoi.
            // _udpSessions la Dictionary thuong, nen moi thao tac doc/ghi phai nam trong lock chung.
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
                // XU LY DA LUONG: lock lan 2 de dang ky session moi mot cach atomic,
                // tranh 2 packet dau tien cua cung client tao 2 UdpProxySession khac nhau.
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
                // MA HOA: giai ma AES de doc ServerId trong UDP_PING/packet.
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
                // LUONG STREAM 1 (ROUTE): day la luong dieu huong nhe, LB doc preface dau tien de biet client dang hoi route.
                // LUONG STREAM 2 (RELAY): neu preface khong phai ROUTE, LB se chuyen sang ky vong proxy TCP/TLS 2 chieu.
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
                // KET NOI DU LIEU/BAT DONG BO: route room co the doc owner_server_id tu database.
                ServerInfo routeTarget = await SelectServerForRouteAsync(roomCode);
                if (routeTarget == null)
                {
                    await SendRouteErrorAsync(clientStream, "no_healthy_server");
                    clientConn.Close();
                    return;
                }

                // XU LY DA LUONG: RoutedClients duoc nhieu task client cap nhat nen phai lock.
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
                // XU LY BAT DONG BO: mo ket noi den backend server bang ConnectAsync.
                await serverConn.ConnectAsync(target.Host, target.TcpPort);

                // XU LY DA LUONG: ActiveProxyConnections la bien dem dung chung cho chon server theo tai.
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

                // XU LY BAT DONG BO: proxy 2 chieu client<->server bang 2 task doc/ghi stream.
                // t1 doc client->server, t2 doc server->client; Task.WhenAny dong phien khi mot huong ngat.
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

            string normalizedRoomCode = roomCode.Trim();
            if (_roomOwnerCache.TryGetValue(normalizedRoomCode, out string cachedOwnerId))
            {
                ServerInfo cachedOwner = SelectServerById(cachedOwnerId);
                if (cachedOwner != null)
                    return cachedOwner;
            }

            string ownerServerId = await GetRoomOwnerServerIdAsync(normalizedRoomCode);
            if (ownerServerId == null)
            {
                Console.WriteLine($"[LB] ROUTE room={normalizedRoomCode} blocked: room was not found in LoadBalancer database");
                return null;
            }

            if (string.IsNullOrWhiteSpace(ownerServerId))
            {
                ServerInfo claimedOwner = await ClaimOwnerForLegacyRoomAsync(normalizedRoomCode);
                if (claimedOwner != null)
                    return claimedOwner;

                Console.WriteLine($"[LB] ROUTE room={normalizedRoomCode} blocked: owner server is empty and no healthy server can claim it");
                return null;
            }

            ServerInfo owner = SelectServerById(ownerServerId);
            if (owner != null)
            {
                _roomOwnerCache[normalizedRoomCode] = owner.ServerId;
                return owner;
            }

            owner = await RefreshAndSelectServerByIdAsync(ownerServerId);
            if (owner != null)
            {
                _roomOwnerCache[normalizedRoomCode] = owner.ServerId;
                return owner;
            }

            Console.WriteLine($"[LB] ROUTE room={normalizedRoomCode} blocked: owner server '{ownerServerId}' is not configured or unhealthy");
            return null;
        }

        private async Task<string> GetRoomOwnerServerIdAsync(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(DatabaseUrl))
                return null;

            try
            {
                // KET NOI DU LIEU: LB ket noi PostgreSQL de doc owner server cua room.
                using var conn = new NpgsqlConnection(PostgresConnectionString.Normalize(DatabaseUrl));
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(
                    "SELECT owner_server_id FROM Rooms WHERE room_code = @room_code LIMIT 1",
                    conn);
                cmd.Parameters.AddWithValue("room_code", roomCode.Trim());
                object result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                    return null;

                return Convert.ToString(result) ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LB] Room owner lookup failed for {roomCode}: {ex.Message}");
                return "";
            }
        }

        private async Task<ServerInfo> ClaimOwnerForLegacyRoomAsync(string roomCode)
        {
            ServerInfo target = SelectServerByRoomHash(roomCode);
            if (target == null)
                return null;

            try
            {
                // KET NOI DU LIEU: room legacy chua co owner se duoc cap nhat owner_server_id trong DB.
                using var conn = new NpgsqlConnection(PostgresConnectionString.Normalize(DatabaseUrl));
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(
                    @"UPDATE Rooms
                      SET owner_server_id = @server_id
                      WHERE room_code = @room_code
                        AND (owner_server_id IS NULL OR owner_server_id = '')",
                    conn);
                cmd.Parameters.AddWithValue("server_id", target.ServerId);
                cmd.Parameters.AddWithValue("room_code", roomCode);
                int updated = await cmd.ExecuteNonQueryAsync();

                if (updated > 0)
                {
                    _roomOwnerCache[roomCode] = target.ServerId;
                    Console.WriteLine($"[LB] ROUTE room={roomCode} claimed legacy owner -> {target.Name}");
                    return target;
                }

                string ownerServerId = await GetRoomOwnerServerIdAsync(roomCode);
                ServerInfo owner = SelectServerById(ownerServerId) ?? await RefreshAndSelectServerByIdAsync(ownerServerId);
                if (owner != null)
                {
                    _roomOwnerCache[roomCode] = owner.ServerId;
                    return owner;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LB] ROUTE room={roomCode} owner claim failed: {ex.Message}");
            }

            return null;
        }

        private ServerInfo SelectServerByRoomHash(string roomCode)
        {
            List<ServerInfo> healthyServers = new List<ServerInfo>();
            lock (_lock)
            {
                foreach (var server in _servers)
                {
                    if (server.IsHealthy)
                        healthyServers.Add(server);
                }
            }

            if (healthyServers.Count == 0)
                return null;

            int hash = 17;
            foreach (char ch in roomCode ?? "")
                hash = unchecked(hash * 31 + char.ToUpperInvariant(ch));

            int safeHash = hash == int.MinValue ? 0 : Math.Abs(hash);
            int index = safeHash % healthyServers.Count;
            return healthyServers[index];
        }

        private async Task<ServerInfo> RefreshAndSelectServerByIdAsync(string serverId)
        {
            if (string.IsNullOrWhiteSpace(serverId))
                return null;

            ServerInfo target = null;
            lock (_lock)
            {
                foreach (var s in _servers)
                {
                    if (string.Equals(s.ServerId, serverId, StringComparison.OrdinalIgnoreCase))
                    {
                        target = s;
                        break;
                    }
                }
            }

            if (target == null)
                return null;

            bool healthy = await PingAsync(target.Host, target.TcpPort);
            lock (_lock)
            {
                target.IsHealthy = healthy;
                target.LastHealthCheck = DateTime.UtcNow;
            }

            return healthy ? target : null;
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
            // LUONG STREAM 1 (ROUTE): response la JSON mot dong ket thuc bang newline de client co the ReadLineAsync.
            // Stream nay chi dung cho dieu huong ban dau, khong phai stream noi dung ung dung.
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
            // LUONG STREAM 1 (ROUTE): loi dieu huong cung phai tra ve newline-terminated JSON de client doc dong cuoi cung.
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
                    // LUONG STREAM 2 (RELAY): doc tung chunk cua NetworkStream; day la proxy binary thuan stream, khong co packet boundary san.
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
                    // LUONG STREAM 2 (RELAY): ghi ngay chunk sang dau con lai de giu duong truyen 2 chieu thong suot.
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
                // XU LY BAT DONG BO: health check chay dinh ky tren background task cua LB.
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
                // XU LY BAT DONG BO: ping backend bang TCP connect co timeout.
                var connectTask = tcp.ConnectAsync(host, port);
                if (await Task.WhenAny(connectTask, Task.Delay(1500)) != connectTask || !tcp.Connected)
                    return false;

                using var ssl = new SslStream(tcp.GetStream(), false, (sender, cert, chain, errors) => true);
                // MA HOA/TLS: LB verify backend bang TLS handshake, khong chi ping port tho.
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
