// ============================================================
// SharedLib/Payloads/GamificationPayload.cs
// Payload cho hệ thống điểm số và leaderboard
// ============================================================
using System.Collections.Generic;

namespace SharedLib.Payloads
{
    /// <summary>Cập nhật điểm real-time cho 1 user.</summary>
    public class ScoreUpdatePayload
    {
        public string RoomCode   { get; set; }
        public string Username   { get; set; }
        public int    Score      { get; set; }  // Tổng điểm hiện tại
        public int    Delta      { get; set; }  // Điểm vừa được cộng
        public string Reason     { get; set; }  // "draw", "sticker", "reaction", "vote"
    }

    /// <summary>Bảng xếp hạng toàn phòng.</summary>
    public class LeaderboardPayload
    {
        public string                  RoomCode { get; set; }
        public List<LeaderboardEntry>  Entries  { get; set; } = new List<LeaderboardEntry>();
    }

    public class LeaderboardEntry
    {
        public int    Rank     { get; set; }
        public string Username { get; set; }
        public int    Score    { get; set; }
    }
}
