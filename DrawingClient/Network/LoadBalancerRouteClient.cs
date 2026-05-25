using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DrawingClient.Network
{
    public sealed class ServerRouteInfo
    {
        public string Host { get; set; } = "127.0.0.1";
        public int TcpPort { get; set; } = 8888;
        public int UdpPort { get; set; } = 8889;
        public string ServerId { get; set; } = "";
        public string ServerName { get; set; } = "";
    }

    public static class LoadBalancerRouteClient
    {
        public static async Task<ServerRouteInfo> ResolveAsync(string lbHost, int lbPort, int timeoutMs = 2500, string roomCode = "")
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(lbHost, lbPort);
            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) != connectTask || !tcp.Connected)
                throw new TimeoutException("Timeout while connecting to load balancer.");

            using var stream = tcp.GetStream();
            stream.ReadTimeout = timeoutMs;
            stream.WriteTimeout = timeoutMs;

            string routeCommand = string.IsNullOrWhiteSpace(roomCode)
                ? "ROUTE\n"
                : $"ROUTE room={roomCode.Trim()}\n";
            byte[] req = Encoding.ASCII.GetBytes(routeCommand);
            await stream.WriteAsync(req, 0, req.Length);
            await stream.FlushAsync();

            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            string line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
                throw new InvalidOperationException("Load balancer returned empty route.");

            JObject json = JObject.Parse(line);
            return new ServerRouteInfo
            {
                Host = json["host"]?.ToString() ?? "127.0.0.1",
                TcpPort = json["tcpPort"]?.ToObject<int?>() ?? 8888,
                UdpPort = json["udpPort"]?.ToObject<int?>() ?? 8889,
                ServerId = json["serverId"]?.ToString() ?? "",
                ServerName = json["serverName"]?.ToString() ?? ""
            };
        }
    }
}
