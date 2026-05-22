// ============================================================
// DrawingServer/Services/SnapshotService.cs
// Tự động chụp snapshot trạng thái canvas mỗi 5 phút
// cho tất cả phòng đang hoạt động.
//
// Cách hoạt động:
//   1. StartAsync() chạy 1 background loop (Task.Run)
//   2. Mỗi 5 phút: duyệt tất cả phòng có người → lấy DrawHistory → lưu Snapshot
//   3. SnapshotID trả về cho client qua SNAPSHOT_LIST để họ có thể restore
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DrawingServer.Database;
using DrawingServer.Network;
using SharedLib.Logging;

namespace DrawingServer.Services
{
    public static class SnapshotService
    {
        private const int INTERVAL_MINUTES = 5;
        private static CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>
        /// Khởi động background loop — gọi 1 lần trong Program.cs khi server start.
        /// </summary>
        public static void StartAsync()
        {
            Task.Run(() => LoopAsync(_cts.Token));
            Logger.Info("SNAP", $"SnapshotService đã khởi động — chụp mỗi {INTERVAL_MINUTES} phút.");
        }

        /// <summary>Dừng snapshot service (dùng khi shutdown server).</summary>
        public static void Stop() => _cts.Cancel();

        // ── Loop chính ─────────────────────────────────────────────────────────

        private static async Task LoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(INTERVAL_MINUTES), ct);
                    await TakeSnapshotAllRoomsAsync();
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    Logger.Warning("SNAP", $"Lỗi trong LoopAsync: {ex.Message}");
                }
            }
        }

        // ── Chụp snapshot tất cả phòng đang có người ───────────────────────────

        private static async Task TakeSnapshotAllRoomsAsync()
        {
            // Lấy danh sách phòng đang có ít nhất 1 client kết nối
            var activeRooms = SecureTcpServer.Clients.Values
                .Where(s => !string.IsNullOrEmpty(s.RoomCode))
                .Select(s => s.RoomCode)
                .Distinct()
                .ToList();

            if (activeRooms.Count == 0)
            {
                Logger.Info("SNAP", "Không có phòng nào đang hoạt động, bỏ qua.");
                return;
            }

            Logger.Info("SNAP", $"Đang chụp snapshot cho {activeRooms.Count} phòng...");

            foreach (string roomCode in activeRooms)
            {
                try
                {
                    await TakeSnapshotForRoomAsync(roomCode);
                }
                catch (Exception ex)
                {
                    Logger.Warning("SNAP", $"Lỗi chụp phòng {roomCode}: {ex.Message}");
                }
            }
        }

        // ── Chụp snapshot cho 1 phòng cụ thể ──────────────────────────────────

        public static async Task<int> TakeSnapshotForRoomAsync(string roomCode, string thumbnailBase64 = "")
        {
            // Lấy toàn bộ DrawHistory hiện tại của phòng
            var history = await DbManager.GetRoomHistoryAsync(roomCode);
            if (history.Count == 0)
            {
                Logger.Info("SNAP", $"Phòng {roomCode} chưa có nét vẽ nào, không chụp.");
                return 0;
            }

            // Gộp thành JSON array: "[{...},{...},...]"
            string snapshotJson = "[" + string.Join(",", history) + "]";

            int snapshotId = await DbManager.SaveSnapshotAsync(roomCode, snapshotJson, thumbnailBase64);

            if (snapshotId > 0)
                Logger.Info("SNAP", $"Phòng {roomCode} → Snapshot #{snapshotId} ({history.Count} nét vẽ)");

            return snapshotId;
        }
    }
}
