using System;
using System.Threading;
using System.Threading.Tasks;
using DrawingServer.Database;
using DrawingServer.Network;
using SharedLib.Config;
using SharedLib.Logging;

namespace DrawingServer.Services
{
    public static class ServerNodeHeartbeatService
    {
        private static CancellationTokenSource? _cts;

        public static void Start(int tcpPort, int udpPort)
        {
            if (_cts != null)
                return;

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => HeartbeatLoopAsync(tcpPort, udpPort, _cts.Token));
        }

        public static void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }

        private static async Task HeartbeatLoopAsync(int tcpPort, int udpPort, CancellationToken token)
        {
            string serverId = EnvLoader.Get("SERVER_ID", "server-1");
            string serverName = EnvLoader.Get("SERVER_NAME", serverId);
            string host = EnvLoader.Get("SERVER_PUBLIC_HOST", "127.0.0.1");
            int maxConnections = Math.Max(1, EnvLoader.GetInt("MAX_TCP_CLIENTS", EnvLoader.GetInt("SERVER_CAPACITY", 200)));
            int intervalMs = Math.Max(1000, EnvLoader.GetInt("HEARTBEAT_INTERVAL_MS", 5000));

            Logger.Info("ServerNode", $"Heartbeat server_id={serverId} host={host} tcp={tcpPort} udp={udpPort}");

            while (!token.IsCancellationRequested)
            {
                await SendHeartbeatAsync(serverId, serverName, host, tcpPort, udpPort, maxConnections);

                try
                {
                    await Task.Delay(intervalMs, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private static Task SendHeartbeatAsync(
            string serverId,
            string serverName,
            string host,
            int tcpPort,
            int udpPort,
            int maxConnections)
        {
            int activeConnections = SecureTcpServer.Clients.Count;
            int activeRooms = RoomService.GetActiveRoomsCount();

            return DbManager.UpsertServerNodeAsync(
                serverId,
                serverName,
                host,
                tcpPort,
                udpPort,
                activeConnections,
                activeRooms,
                maxConnections,
                isHealthy: true);
        }
    }
}
