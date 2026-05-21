// ============================================================
// DrawingClient/AI/RemoveBgClient.cs
// Tuần 5 — Xóa nền ảnh qua remove.bg API
// Free tier: 50 ảnh/tháng
// ============================================================
using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using SharedLib.AI;

namespace DrawingClient.AI
{
    public static class RemoveBgClient
    {
        /// <summary>
        /// Gửi ảnh lên remove.bg → nhận PNG trong suốt (nền đã xóa).
        /// inputImageBytes: byte[] PNG hoặc JPEG
        /// Trả về byte[] PNG trong suốt, hoặc null nếu lỗi.
        /// </summary>
        public static async Task<byte[]> RemoveBackgroundAsync(byte[] inputImageBytes)
        {
            if (!ApiConfig.IsRemoveBgConfigured())
                throw new InvalidOperationException("Chưa cấu hình remove.bg API key trong ApiConfig.cs");

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("X-Api-Key", ApiConfig.RemoveBgApiKey);

            var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(inputImageBytes), "image_file", "input.png");
            content.Add(new StringContent("auto"), "size");   // auto = tốt nhất
            content.Add(new StringContent("rgba"), "format"); // nhận PNG với alpha channel

            HttpResponseMessage response = await client.PostAsync(ApiConfig.RemoveBgUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[RemoveBgClient] Lỗi {response.StatusCode}: {error}");
                return null;
            }

            // remove.bg trả về trực tiếp byte[] PNG (không phải JSON)
            return await response.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Xóa nền từ Bitmap, trả về Bitmap mới với nền trong suốt.
        /// </summary>
        public static async Task<Bitmap> RemoveBackgroundAsync(Bitmap source)
        {
            using var ms = new MemoryStream();
            source.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] inputBytes = ms.ToArray();

            byte[] resultBytes = await RemoveBackgroundAsync(inputBytes);
            if (resultBytes == null) return null;

            using var resultMs = new MemoryStream(resultBytes);
            return new Bitmap(resultMs);
        }
    }
}
