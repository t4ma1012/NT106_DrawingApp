// ============================================================
// SharedLib/Payloads/GalleryPayload.cs
// ============================================================
using System;
using System.Collections.Generic;

namespace SharedLib.Payloads
{
    public class SaveGalleryPayload
    {
        public string RoomCode { get; set; }
        public string Username { get; set; }
        public string Filename { get; set; }
        public string ImageData { get; set; }       // base64 PNG
        public string ThumbnailData { get; set; }   // base64 PNG 200x150
        public bool IsAiGenerated { get; set; } = false;
    }

    public class GetGalleryPayload
    {
        public string RoomCode { get; set; }
    }

    public class GalleryResponsePayload
    {
        public string RoomCode { get; set; }
        public List<GalleryItem> Items { get; set; } = new List<GalleryItem>();
    }

    public class GalleryItem
    {
        public int ID { get; set; }
        public string Filename { get; set; }
        public string ThumbnailData { get; set; }   // base64
        public string SavedBy { get; set; }
        public long SavedAt { get; set; }            // Unix ms
        public bool IsAiGenerated { get; set; }
        public string PublicLink { get; set; }       // Tuần 7 — public URL
    }

    // Tuần 7 — Public Gallery Link
    public class PublicGalleryLinkPayload
    {
        public int GalleryItemID { get; set; }
        public string PublicToken { get; set; }
        public string PublicUrl { get; set; }
    }
}
