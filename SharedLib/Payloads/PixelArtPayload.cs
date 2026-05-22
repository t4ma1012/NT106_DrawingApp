using System.Collections.Generic;

namespace SharedLib.Payloads
{
    public class PixelArtDrawPayload
    {
        public string Username  { get; set; }
        public string RoomCode  { get; set; }
        public int    Row       { get; set; }
        public int    Col       { get; set; }
        public int    ColorARGB { get; set; }
        public long   Timestamp { get; set; }
    }

    public class PixelArtSyncPayload
    {
        public string          RoomCode { get; set; }
        public int             GridSize { get; set; }
        public List<PixelCell> Cells    { get; set; } = new List<PixelCell>();
    }

    public class PixelCell
    {
        public int Row       { get; set; }
        public int Col       { get; set; }
        public int ColorARGB { get; set; }
    }
}
