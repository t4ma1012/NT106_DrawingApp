// ============================================================
// SharedLib/Payloads/GifExportPayload.cs
// Tuần 7 — GIF Export payloads
// ============================================================

namespace SharedLib.Payloads
{
    /// <summary>Client yêu cầu xuất GIF từ DrawHistory.</summary>
    public class GifExportRequestPayload
    {
        public string RoomCode { get; set; }
        public int FpsFrames { get; set; } = 10;  // 10 FPS = 100ms per frame
        public string Filename { get; set; }      // "drawing.gif"
        public long StartTimestamp { get; set; }   // Unix ms (0 = từ đầu)
        public long EndTimestamp { get; set; }     // Unix ms (0 = đến hiện tại)
    }

    /// <summary>Server gửi tiến độ xuất GIF.</summary>
    public class GifExportProgressPayload
    {
        public string RoomCode { get; set; }
        public int ProgressPercent { get; set; }  // 0-100
        public string Status { get; set; }        // "starting", "processing", "completed", "error"
        public string ErrorMessage { get; set; }  // nếu status = "error"
        public string GifData { get; set; }       // base64 GIF file khi completed
        public int FileSize { get; set; }         // bytes
    }
}
