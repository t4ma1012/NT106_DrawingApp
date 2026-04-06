// ============================================================
// SharedLib/Payloads/AiPayload.cs
// Tuần 5-6 — Tất cả payload liên quan đến tính năng AI
// ============================================================

namespace SharedLib.Payloads
{
    // ── Tuần 5: Text-to-Drawing ─────────────────────────────
    public class AiTextToImageRequestPayload
    {
        public string RequesterUsername { get; set; }
        public string Prompt { get; set; }
        public int TargetX { get; set; }  // vị trí dán ảnh lên canvas
        public int TargetY { get; set; }
    }

    /// <summary>Server broadcast kết quả cho cả phòng.</summary>
    public class AiTextToImageResultPayload
    {
        public string RequesterUsername { get; set; }
        public string ActionID { get; set; }
        public string ImageData { get; set; }   // base64 PNG
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Timestamp { get; set; }
    }

    // ── Tuần 5: Background Remover ──────────────────────────
    public class AiBgRemovedPayload
    {
        public string RequesterUsername { get; set; }
        public string ActionID { get; set; }
        public string ImageData { get; set; }   // base64 PNG trong suốt
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Timestamp { get; set; }
    }

    // ── Tuần 6: Magic Eraser ────────────────────────────────
    public class AiMagicEraseRequestPayload
    {
        public string RequesterUsername { get; set; }
        public string OriginalImageData { get; set; }  // base64 PNG toàn canvas
        public string MaskImageData { get; set; }      // base64 PNG mask đen/trắng
        public int RegionX { get; set; }
        public int RegionY { get; set; }
        public int RegionWidth { get; set; }
        public int RegionHeight { get; set; }
    }

    public class AiMagicEraseResultPayload
    {
        public string RequesterUsername { get; set; }
        public string ActionID { get; set; }
        public string ResultImageData { get; set; }  // base64 PNG đã xóa
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Timestamp { get; set; }
    }

    // ── Tuần 6: Auto-Complete Nét Vẽ ────────────────────────
    public class AiAutoCompleteRequestPayload
    {
        public string RequesterUsername { get; set; }
        public string OriginalImageData { get; set; }
        public string MaskImageData { get; set; }
        public int RegionX { get; set; }
        public int RegionY { get; set; }
        public int RegionWidth { get; set; }
        public int RegionHeight { get; set; }
    }

    public class AiAutoCompleteResultPayload
    {
        public string RequesterUsername { get; set; }
        public string ActionID { get; set; }
        public string ResultImageData { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Timestamp { get; set; }
    }
}
