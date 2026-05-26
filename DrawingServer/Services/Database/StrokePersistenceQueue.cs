using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SharedLib.Logging;

namespace DrawingServer.Database
{
    public static class StrokePersistenceQueue
    {
        private sealed class StrokeRecord
        {
            public long Sequence;
            public int RoomVersion;
            public string RoomCode = "";
            public string ActionId = "";
            public string StrokeJson = "";
            public string Username = "";
        }

        // XU LY DA LUONG: BlockingCollection lam hang doi an toan giua thread realtime va worker luu DB.
        private static readonly BlockingCollection<StrokeRecord> Queue = new BlockingCollection<StrokeRecord>();
        private static readonly Dictionary<string, List<StrokeRecord>> PendingByRoom = new Dictionary<string, List<StrokeRecord>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> RoomVersions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly object PendingLock = new object();
        private static long _nextSequence;
        private static int _workerStarted;

        public static void Enqueue(string roomCode, string actionId, string strokeJson, string username)
        {
            if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(strokeJson))
                return;

            EnsureWorkerStarted();

            var record = new StrokeRecord
            {
                // XU LY DA LUONG: Interlocked tao so thu tu thread-safe cho stroke moi.
                Sequence = Interlocked.Increment(ref _nextSequence),
                RoomCode = roomCode,
                ActionId = string.IsNullOrWhiteSpace(actionId) ? Guid.NewGuid().ToString() : actionId,
                StrokeJson = strokeJson,
                Username = username ?? ""
            };

            // XU LY DA LUONG: khoa pending theo room de nhieu packet ve khong sua list cung luc.
            lock (PendingLock)
            {
                if (!RoomVersions.TryGetValue(roomCode, out int version))
                    version = 0;
                record.RoomVersion = version;

                if (!PendingByRoom.TryGetValue(roomCode, out var list))
                {
                    list = new List<StrokeRecord>();
                    PendingByRoom[roomCode] = list;
                }
                list.Add(record);
            }

            // XU LY BAT DONG BO: dua stroke vao queue, worker nen se luu DB sau.
            Queue.Add(record);
        }

        public static List<string> GetPendingStrokeJson(string roomCode)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(roomCode))
                return result;

            lock (PendingLock)
            {
                if (!PendingByRoom.TryGetValue(roomCode, out var list))
                    return result;

                list.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
                foreach (var record in list)
                    result.Add(record.StrokeJson);
            }

            return result;
        }

        public static void ClearRoom(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
                return;

            lock (PendingLock)
            {
                RoomVersions[roomCode] = RoomVersions.TryGetValue(roomCode, out int version) ? version + 1 : 1;
                PendingByRoom.Remove(roomCode);
            }
        }

        private static void EnsureWorkerStarted()
        {
            if (Interlocked.Exchange(ref _workerStarted, 1) == 1)
                return;

            // XU LY DA LUONG: khoi dong worker nen mot lan duy nhat de xu ly hang doi DB.
            Task.Run(ProcessQueueAsync);
        }

        private static async Task ProcessQueueAsync()
        {
            foreach (var record in Queue.GetConsumingEnumerable())
            {
                if (IsStale(record))
                    continue;

                int delayMs = 250;
                while (!IsStale(record))
                {
                    // KET NOI DU LIEU/BAT DONG BO: worker goi DB async, retry neu database tam thoi loi/qua tai.
                    bool saved = await DbManager.SaveStrokeRecordAsync(record.RoomCode, record.ActionId, record.StrokeJson, record.Username);
                    if (saved)
                    {
                        RemovePending(record);
                        break;
                    }

                    Logger.Warning("DB", $"[SAVE RETRY] room={record.RoomCode} action={record.ActionId} in {delayMs}ms");
                    await Task.Delay(delayMs);
                    delayMs = Math.Min(delayMs * 2, 5000);
                }
            }
        }

        private static bool IsStale(StrokeRecord record)
        {
            lock (PendingLock)
            {
                return RoomVersions.TryGetValue(record.RoomCode, out int version) && version != record.RoomVersion;
            }
        }

        private static void RemovePending(StrokeRecord record)
        {
            lock (PendingLock)
            {
                if (!PendingByRoom.TryGetValue(record.RoomCode, out var list))
                    return;

                list.RemoveAll(x => x.Sequence == record.Sequence);
                if (list.Count == 0)
                    PendingByRoom.Remove(record.RoomCode);
            }
        }
    }
}
