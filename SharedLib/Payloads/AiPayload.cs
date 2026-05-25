namespace SharedLib.Payloads
{
    public class AiTextToImageRequestPayload
    {
        public string RequesterUsername { get; set; }
        public string Prompt { get; set; }
        public int TargetX { get; set; }
        public int TargetY { get; set; }
    }

    public class AiTextToImageResultPayload
    {
        public string RequesterUsername { get; set; }
        public string ActionID { get; set; }
        public string Prompt { get; set; }
        public string ImageData { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Timestamp { get; set; }
    }

    public class AiBgRemovedPayload
    {
        public string RequesterUsername { get; set; }
        public string ActionID { get; set; }
        public string ImageData { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Timestamp { get; set; }
    }

}
