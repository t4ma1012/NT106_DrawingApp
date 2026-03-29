// ============================================================
// SharedLib/Payloads/SyncPayload.cs
// ============================================================
using System.Collections.Generic;

namespace SharedLib.Payloads
{
    /// <summary>Đại diện một hành động vẽ đã được lưu (dùng cho sync & playback).</summary>
    public class DrawAction
    {
        public string ActionID { get; set; }
        public string Username { get; set; }
        public string ToolType { get; set; }
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public int ColorARGB { get; set; }
        public int Thickness { get; set; }
        public string Text { get; set; }
        public string FontName { get; set; }
        public int FontSize { get; set; }
        public string ImageData { get; set; }  // base64, dùng cho ImportImage
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public long Timestamp { get; set; }
        public bool IsAiGenerated { get; set; } = false;
    }

    public class SyncBoardPayload
    {
        public string RoomCode { get; set; }
        public List<DrawAction> Actions { get; set; } = new List<DrawAction>();
    }

    public class UndoPayload
    {
        public string ActionID { get; set; }
        public string Username { get; set; }
    }

    public class RedoPayload
    {
        public string ActionID { get; set; }
        public string Username { get; set; }
    }

    public class PlaybackRequestPayload
    {
        public string RoomCode { get; set; }
    }

    public class PlaybackResponsePayload
    {
        public string RoomCode { get; set; }
        public List<DrawAction> Actions { get; set; } = new List<DrawAction>();
    }

    // Tuần 6 — Time Travel Timeline
    public class TimelineRequestPayload
    {
        public string RoomCode { get; set; }
        public long TargetTimestamp { get; set; }  // Unix ms — muốn xem canvas tại thời điểm này
    }

    public class TimelineResponsePayload
    {
        public string RoomCode { get; set; }
        public long TargetTimestamp { get; set; }
        public List<DrawAction> Actions { get; set; } = new List<DrawAction>();
    }

    // Tuần 6 — Snapshot
    public class SnapshotListPayload
    {
        public string RoomCode { get; set; }
        public List<SnapshotInfo> Snapshots { get; set; } = new List<SnapshotInfo>();
    }

    public class SnapshotInfo
    {
        public int SnapshotID { get; set; }
        public long Timestamp { get; set; }
        public string ThumbnailBase64 { get; set; }
    }

    public class SnapshotRestorePayload
    {
        public string RoomCode { get; set; }
        public int SnapshotID { get; set; }
    }
}
