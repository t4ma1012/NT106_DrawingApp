using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DrawingServer.Network;
using Newtonsoft.Json;
using Npgsql;
using SharedLib.Config;
using SharedLib.Logging;
using SharedLib.Packets;

namespace DrawingServer.Services
{
    public static class CrossServerSyncService
    {
        private const string NotifyChannel = "room_events";
        private static readonly ConcurrentDictionary<string, byte> ProcessedEvents = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        private static string _connString = "";
        private static string _serverId = "";
        private static NpgsqlConnection? _listenConn;
        private static CancellationTokenSource? _cts;
        private static volatile bool _started;
        private static readonly SemaphoreSlim PublishLock = new SemaphoreSlim(1, 1);

        private sealed class NotifyPayload
        {
            public string event_id { get; set; } = "";
            public string source_server_id { get; set; } = "";
            public string room_code { get; set; } = "";
            public int cmd { get; set; }
            public string payload_base64 { get; set; } = "";
        }

        public static void Start()
        {
            if (_started)
                return;

            _connString = PostgresConnectionString.Normalize(EnvLoader.Get("DATABASE_URL", ""));
            _serverId = EnvLoader.Get("SERVER_ID", "server-1");
            if (string.IsNullOrWhiteSpace(_connString))
            {
                Logger.Warning("CrossSync", "DATABASE_URL is missing. Cross-server sync disabled.");
                return;
            }

            _cts = new CancellationTokenSource();
            _started = true;
            _ = Task.Run(() => ListenLoopAsync(_cts.Token));
            Logger.Info("CrossSync", $"Started LISTEN/NOTIFY on channel '{NotifyChannel}' as {_serverId}");
        }

        public static void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listenConn?.Close(); } catch { }
            _started = false;
        }

        public static async Task PublishEventAsync(string roomCode, Packet packet, string sourceUsername = "")
        {
            if (!_started || string.IsNullOrWhiteSpace(roomCode) || packet == null)
                return;

            string eventId = Guid.NewGuid().ToString("N");
            string payloadBase64 = Convert.ToBase64String(packet.Serialize());
            var notify = new NotifyPayload
            {
                event_id = eventId,
                source_server_id = _serverId,
                room_code = roomCode,
                cmd = (int)packet.Cmd,
                payload_base64 = payloadBase64
            };
            string notifyJson = JsonConvert.SerializeObject(notify);

            await PublishLock.WaitAsync();
            try
            {
                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                string sql = @"
INSERT INTO RoomEvents (room_id, event_id, event_type, payload, created_by, source_server_id, created_at)
SELECT r.id, @event_id::uuid, @event_type, @payload::jsonb, @created_by, @source_server_id, NOW()
FROM Rooms r WHERE r.room_code = @room_code;
SELECT pg_notify(@channel, @notify_payload);";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("event_id", eventId);
                cmd.Parameters.AddWithValue("event_type", packet.Cmd.ToString());
                cmd.Parameters.AddWithValue("payload", notifyJson);
                cmd.Parameters.AddWithValue("created_by", sourceUsername ?? "");
                cmd.Parameters.AddWithValue("source_server_id", _serverId);
                cmd.Parameters.AddWithValue("room_code", roomCode);
                cmd.Parameters.AddWithValue("channel", NotifyChannel);
                cmd.Parameters.AddWithValue("notify_payload", notifyJson);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Logger.Warning("CrossSync", $"Publish failed: {ex.Message}");
            }
            finally
            {
                PublishLock.Release();
            }
        }

        private static async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    _listenConn = new NpgsqlConnection(_connString);
                    await _listenConn.OpenAsync(token);
                    _listenConn.Notification += OnNotification;

                    using (var listenCmd = new NpgsqlCommand($"LISTEN {NotifyChannel};", _listenConn))
                    {
                        await listenCmd.ExecuteNonQueryAsync(token);
                    }

                    while (!token.IsCancellationRequested)
                    {
                        _listenConn.Wait();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warning("CrossSync", $"Listen loop error: {ex.Message}");
                    await Task.Delay(1000, token);
                }
                finally
                {
                    try
                    {
                        if (_listenConn != null)
                            _listenConn.Notification -= OnNotification;
                    }
                    catch { }
                }
            }
        }

        private static void OnNotification(object sender, NpgsqlNotificationEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(e.Payload))
                    return;

                NotifyPayload? payload = JsonConvert.DeserializeObject<NotifyPayload>(e.Payload);
                if (payload == null || string.IsNullOrWhiteSpace(payload.event_id))
                    return;

                if (string.Equals(payload.source_server_id, _serverId, StringComparison.OrdinalIgnoreCase))
                    return;

                if (!ProcessedEvents.TryAdd(payload.event_id, 1))
                    return;

                Packet packet = Packet.Deserialize(Convert.FromBase64String(payload.payload_base64));
                _ = SecureTcpServer.BroadcastPacketToRoomStaticAsync(payload.room_code, packet);
            }
            catch (Exception ex)
            {
                Logger.Warning("CrossSync", $"Notify handler error: {ex.Message}");
            }
        }
    }
}
