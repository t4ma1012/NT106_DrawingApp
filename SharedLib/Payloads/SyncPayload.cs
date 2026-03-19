using System;
using System.Collections.Generic;

namespace SharedLib.Payloads
{
    // Đại diện cho 1 nét vẽ đã được xác nhận — lưu DB + dùng cho Undo/Sync
    public class DrawAction
    {
        public string ActionID { get; set; }         // GUID, dùng để Undo đúng nét
        public string Username { get; set; }
        public string ToolType { get; set; }         // "pen","line","rect","circle"...
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public int ColorARGB { get; set; }
        public int Thickness { get; set; }
        public string Text { get; set; }             // nếu là Text Tool
        public string FontName { get; set; }
        public int FontSize { get; set; }
        public long Timestamp { get; set; }
    }

    // CMD_SYNC_BOARD — server gửi toàn bộ lịch sử khi client mới join
    public class SyncBoardPayload
    {
        public string RoomCode { get; set; }
        public List<DrawAction> Actions { get; set; } = new List<DrawAction>();
    }

    // CMD_UNDO — chỉ xóa đúng nét của người đó theo ActionID
    public class UndoPayload
    {
        public string ActionID { get; set; }
        public string Username { get; set; }
    }

    // CMD_REDO
    public class RedoPayload
    {
        public string ActionID { get; set; }
        public string Username { get; set; }
    }
}