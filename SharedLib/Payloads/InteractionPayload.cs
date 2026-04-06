// ============================================================
// SharedLib/Payloads/InteractionPayload.cs
// Tất cả payload tương tác: cursor, laser, emoji, chat, log,
// spotlight, follow, sticker, sticky note, vote (Tuần 2-6)
// ============================================================
using System.Collections.Generic;

namespace SharedLib.Payloads
{
    // ── Tuần 2 ──────────────────────────────────────────────

    public class CursorPayload
    {
        public string Username { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class LaserPayload
    {
        public string Username { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsActive { get; set; }  // false = nhả Alt → xóa laser
    }

    public class ReactionPayload
    {
        public string Username { get; set; }
        public string Emoji { get; set; }   // "👍", "❤️", "😂"
        public int X { get; set; }
        public int Y { get; set; }
    }

    // ── Tuần 3 ──────────────────────────────────────────────

    public class ChatPayload
    {
        public string Username { get; set; }
        public int ColorARGB { get; set; }
        public string Message { get; set; }
        public long Timestamp { get; set; }
    }

    public class ActivityLogPayload
    {
        public string Username { get; set; }
        public string Action { get; set; }  // "joined", "left", "drew", "undo", "flood_fill", etc.
        public long Timestamp { get; set; }
    }

    // ── Tuần 5 ──────────────────────────────────────────────

    /// <summary>Sticker & Shape Library — kéo thả hình dán vào canvas.</summary>
    public class StickerPayload
    {
        public string ActionID { get; set; }
        public string Username { get; set; }
        public string StickerID { get; set; }  // "heart", "star", "arrow", "emoji_happy", ...
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float Rotation { get; set; }    // độ
        public long Timestamp { get; set; }
    }

    /// <summary>Follow Mode — theo dõi vị trí/zoom của người khác realtime.</summary>
    public class FollowModePayload
    {
        public string FollowerUsername { get; set; }
        public string TargetUsername { get; set; }
        public bool IsFollowing { get; set; }
        // Khi leader gửi vị trí viewport:
        public int ViewX { get; set; }
        public int ViewY { get; set; }
        public float ZoomFactor { get; set; }
    }

    /// <summary>Spotlight Mode — UDP — vùng sáng quanh chuột, phần còn lại tối.</summary>
    public class SpotlightPayload
    {
        public string Username { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsActive { get; set; }
        public int RadiusPx { get; set; } = 200;
    }

    /// <summary>Sticky Note / Comment — giống Figma comment.</summary>
    public class StickyNotePayload
    {
        public string NoteID { get; set; }
        public string AuthorUsername { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Text { get; set; }
        public bool IsOpen { get; set; } = true;
        public long Timestamp { get; set; }
    }

    public class StickyNoteReplyPayload
    {
        public string NoteID { get; set; }
        public string AuthorUsername { get; set; }
        public string Text { get; set; }
        public long Timestamp { get; set; }
    }

    // ── Tuần 6 ──────────────────────────────────────────────

    /// <summary>Voting / Like nét vẽ — hover vào vùng → nhấn 👍 vote.</summary>
    public class VoteDrawPayload
    {
        public string ActionID { get; set; }   // ActionID của nét vẽ được vote
        public string VoterUsername { get; set; }
    }

    public class VoteResponsePayload
    {
        public string ActionID { get; set; }
        public int TotalVotes { get; set; }
        public List<string> Voters { get; set; } = new List<string>();
    }

    // ── Tuần 7-8 ────────────────────────────────────────────

    /// <summary>Drawing Prompt — server gửi chủ đề, đếm ngược, vote.</summary>
    public class DrawingPromptPayload
    {
        public string PromptText { get; set; }
        public int CountdownSeconds { get; set; } = 60;
        public bool IsStart { get; set; }
        public bool IsEnd { get; set; }
    }

    /// <summary>Blind Drawing Mode — mỗi người chỉ thấy phần mình vẽ.</summary>
    public class BlindDrawPayload
    {
        public bool IsReveal { get; set; }  // false = start, true = reveal tất cả
        public string RoomCode { get; set; }
    }

    /// <summary>Pixel Art — Tuần 8 — UDP sync ô pixel.</summary>
    public class PixelArtDrawPayload
    {
        public string Username { get; set; }
        public int Row { get; set; }
        public int Col { get; set; }
        public int ColorARGB { get; set; }
        public long Timestamp { get; set; }
    }

    public class PixelArtSyncPayload
    {
        public string RoomCode { get; set; }
        public int GridSize { get; set; }  // 32 hoặc 64
        public int[] Grid { get; set; }    // Mảng phẳng [Row * GridSize + Col] = ColorARGB
    }
}
