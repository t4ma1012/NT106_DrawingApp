// ============================================================
// SharedLib/Payloads/DrawPayload.cs
// ============================================================
namespace SharedLib.Payloads
{
    public class DrawPayload
    {
        public string ActionID { get; set; }      // GUID — unique per stroke
        public string Username { get; set; }
        public string ToolType { get; set; }      // "Pen","Line","Rectangle","Circle","Eraser","FloodFill","Text"
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public int ColorARGB { get; set; }
        public int Thickness { get; set; }
        public string Text { get; set; }          // Dùng khi ToolType = "Text"
        public string FontName { get; set; }
        public int FontSize { get; set; }
        public long Timestamp { get; set; }       // Unix ms
    }

    public class FloodFillPayload
    {
        public string ActionID { get; set; }
        public string Username { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int ColorARGB { get; set; }
        public long Timestamp { get; set; }
    }

    public class ImportImagePayload
    {
        public string ActionID { get; set; }
        public string Username { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string ImageData { get; set; }    // base64 JPEG 70%
        public long Timestamp { get; set; }
    }

    public class SetBackgroundPayload
    {
        public string Username { get; set; }
        public int ColorARGB { get; set; }
    }
}
