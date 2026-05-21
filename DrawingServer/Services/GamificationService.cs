// ============================================================
// DrawingServer/Services/GamificationService.cs
// Hệ thống trò chơi hóa: tính điểm, leaderboard theo phòng
//
// Cách tính điểm:
//   +1  mỗi nét vẽ gửi lên (DrawPayload)
//   +5  khi nhận được reaction từ người khác
//   +10 khi được vote (VoteDrawPayload)
//   +3  khi đặt sticker
// ============================================================
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DrawingServer.Services
{
    public static class GamificationService
    {
        // roomCode -> (username -> score)
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>>
            _scores = new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>();

        // Điểm thưởng cho từng hành động
        public const int POINTS_DRAW    = 1;
        public const int POINTS_STICKER = 3;
        public const int POINTS_REACT   = 5;
        public const int POINTS_VOTE    = 10;

        /// <summary>Đảm bảo phòng tồn tại trong hệ thống điểm.</summary>
        private static ConcurrentDictionary<string, int> GetRoom(string roomCode)
        {
            return _scores.GetOrAdd(roomCode, _ => new ConcurrentDictionary<string, int>());
        }

        /// <summary>Cộng điểm cho user trong phòng.</summary>
        public static int AddScore(string roomCode, string username, int points)
        {
            if (string.IsNullOrEmpty(roomCode) || string.IsNullOrEmpty(username) || points <= 0)
                return 0;

            var room = GetRoom(roomCode);
            int newScore = room.AddOrUpdate(username, points, (_, old) => old + points);

            SharedLib.Logging.Logger.Info("GAME",
                $"{username} +{points} điểm trong phòng {roomCode} → tổng: {newScore}");

            return newScore;
        }

        /// <summary>Lấy điểm hiện tại của user trong phòng.</summary>
        public static int GetScore(string roomCode, string username)
        {
            if (_scores.TryGetValue(roomCode, out var room))
                if (room.TryGetValue(username, out int score))
                    return score;
            return 0;
        }

        /// <summary>
        /// Lấy leaderboard của phòng, sắp xếp giảm dần theo điểm.
        /// Trả về danh sách (username, score, rank).
        /// </summary>
        public static List<LeaderboardEntry> GetLeaderboard(string roomCode)
        {
            var result = new List<LeaderboardEntry>();

            if (!_scores.TryGetValue(roomCode, out var room))
                return result;

            int rank = 1;
            foreach (var kv in room.OrderByDescending(x => x.Value))
            {
                result.Add(new LeaderboardEntry
                {
                    Username = kv.Key,
                    Score = kv.Value,
                    Rank = rank++
                });
            }
            return result;
        }

        /// <summary>Xoá điểm khi phòng bị đóng.</summary>
        public static void ClearRoom(string roomCode)
        {
            _scores.TryRemove(roomCode, out _);
        }

        /// <summary>Đảm bảo user được khởi tạo với 0 điểm khi join phòng.</summary>
        public static void EnsureUser(string roomCode, string username)
        {
            if (string.IsNullOrEmpty(roomCode) || string.IsNullOrEmpty(username)) return;
            var room = GetRoom(roomCode);
            room.TryAdd(username, 0);
        }
    }

    public class LeaderboardEntry
    {
        public string Username { get; set; }
        public int Score { get; set; }
        public int Rank { get; set; }
    }
}
