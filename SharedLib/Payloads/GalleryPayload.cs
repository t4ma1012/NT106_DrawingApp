using System;
using System.Collections.Generic;

namespace SharedLib.Payloads
{
    // CMD_SAVE_TO_GALLERY — A gửi lên sau khi export PNG
    public class SaveGalleryPayload
    {
        public string RoomCode { get; set; }
        public string SavedBy { get; set; }
        public string Filename { get; set; }        // "NT106_<roomcode>_<timestamp>.png"
        public string ImageData { get; set; }       // base64 PNG gốc
        public string ThumbnailData { get; set; }   // base64 PNG thu nhỏ 200×150
    }

    // CMD_GET_GALLERY — A gửi khi mở tab Gallery trong Lobby
    public class GetGalleryPayload
    {
        public string RoomCode { get; set; }
    }

    // CMD_GALLERY_RESPONSE — server trả danh sách ảnh
    public class GalleryResponsePayload
    {
        public string RoomCode { get; set; }
        public List<GalleryItem> Items { get; set; } = new List<GalleryItem>();
    }

    public class GalleryItem
    {
        public int Id { get; set; }
        public string Filename { get; set; }
        public string SavedBy { get; set; }
        public long SavedAt { get; set; }           // Unix timestamp ms
        public string ThumbnailData { get; set; }   // base64, A dùng để render thumbnail
    }
}