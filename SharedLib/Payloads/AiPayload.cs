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
