namespace SharedLib.Payloads
{
    // CMD_CURSOR / CMD_LASER — gửi qua UDP
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
        public bool IsActive { get; set; }
    }

    // CMD_REACTION
    public class ReactionPayload
    {
        public string Username { get; set; }
        public int EmojiType { get; set; } // 1=👍 2=❤️ 3=😂
        public int X { get; set; }
        public int Y { get; set; }
    }

    // CMD_CHAT
    public class ChatPayload
    {
        public string Username { get; set; }
        public string Message { get; set; }
        public long Timestamp { get; set; }
    }

    // CMD_ACTIVITY_LOG
    public class ActivityLogPayload
    {
        public string Username { get; set; }
        public string Action { get; set; }
        public long Timestamp { get; set; }
    }
}