using System;

namespace SharedLib.Payloads
{
    // Dùng cho CMD_DRAW — gửi qua UDP
    public class DrawPayload
    {
        public string ActionID { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; }
        public string ToolType { get; set; } // "pen","line","rect","circle","eraser"
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public int ColorARGB { get; set; } // dùng Color.ToArgb()
        public int Thickness { get; set; }
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}