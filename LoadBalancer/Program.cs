using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SharedLib.Config;

namespace LoadBalancer
{
    class Program
    {
        private sealed class RouteServerConfig
        {
            public string server_id { get; set; } = "";
            public string name { get; set; } = "";
            public string host { get; set; } = "";
            public int tcp_port { get; set; }
            public int udp_port { get; set; }
        }

        static async Task Main(string[] args)
        {
            EnvLoader.Load();

            int listenPort = EnvLoader.GetInt("LOAD_BALANCER_PORT", 9000);
            int udpPort = EnvLoader.GetInt("LOAD_BALANCER_UDP_PORT", 9001);
            string strategy = EnvLoader.Get("LOAD_BALANCER_STRATEGY", "room-affinity");
            var lb = new DrawingLoadBalancer();
            lb.RoutingStrategy = string.IsNullOrWhiteSpace(strategy) ? "room-affinity" : strategy.Trim();
            lb.DatabaseUrl = EnvLoader.Get("DATABASE_URL", "");

            int added = TryLoadServersFromJson(lb);
            if (added == 0)
            {
                AddServersFromEnv(lb);
            }

            Console.WriteLine($"[LB] Routing strategy: {lb.RoutingStrategy}");
            Console.WriteLine("[LB] Ctrl+C to stop.");
            await lb.StartAsync(listenPort, udpPort);
        }

        private static int TryLoadServersFromJson(DrawingLoadBalancer lb)
        {
            string[] candidates =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "servers.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "servers.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "LoadBalancer", "servers.json")
            };

            foreach (string path in candidates)
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    string json = File.ReadAllText(path);
                    var servers = JsonConvert.DeserializeObject<List<RouteServerConfig>>(json);
                    if (servers == null || servers.Count == 0)
                        continue;

                    foreach (var s in servers)
                    {
                        lb.AddServer(
                            s.host,
                            s.tcp_port,
                            s.udp_port,
                            string.IsNullOrWhiteSpace(s.name) ? s.server_id : s.name,
                            s.server_id);
                    }

                    Console.WriteLine($"[LB] Loaded {servers.Count} server(s) from {path}");
                    return servers.Count;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LB] Cannot parse {path}: {ex.Message}");
                }
            }

            return 0;
        }

        private static void AddServersFromEnv(DrawingLoadBalancer lb)
        {
            string h1 = EnvLoader.Get("LB_SERVER_1_HOST", "127.0.0.1");
            int t1 = EnvLoader.GetInt("LB_SERVER_1_TCP_PORT", 8888);
            int u1 = EnvLoader.GetInt("LB_SERVER_1_UDP_PORT", 8889);
            string n1 = EnvLoader.Get("LB_SERVER_1_NAME", "DrawingServer-1");
            string id1 = EnvLoader.Get("LB_SERVER_1_ID", "server-1");
            lb.AddServer(h1, t1, u1, n1, id1);

            string h2 = EnvLoader.Get("LB_SERVER_2_HOST", "127.0.0.1");
            int t2 = EnvLoader.GetInt("LB_SERVER_2_TCP_PORT", 8890);
            int u2 = EnvLoader.GetInt("LB_SERVER_2_UDP_PORT", 8891);
            string n2 = EnvLoader.Get("LB_SERVER_2_NAME", "DrawingServer-2");
            string id2 = EnvLoader.Get("LB_SERVER_2_ID", "server-2");

            bool hasSecond = !(string.IsNullOrWhiteSpace(h2) || (h2 == h1 && t2 == t1));
            if (hasSecond)
            {
                lb.AddServer(h2, t2, u2, n2, id2);
            }
        }
    }
}
