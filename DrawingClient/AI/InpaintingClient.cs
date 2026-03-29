// ============================================================
// DrawingClient/AI/InpaintingClient.cs
// Tuần 6 — Dùng chung cho cả Magic Eraser VÀ Auto-Complete
// Stability AI inpainting: nhận original PNG + mask PNG → trả về kết quả PNG
// mask: vùng ĐEN = giữ nguyên, vùng TRẮNG = AI fill vào
// ============================================================
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SharedLib.AI;

namespace DrawingClient.AI
{
    public static class InpaintingClient
    {
        // ── Tuần 6: Magic Eraser / Auto-Complete ────────────────

        /// <summary>
        /// Gọi Stability AI inpainting endpoint.
        /// originalPng: byte[] PNG toàn bộ canvas (hoặc vùng crop)
        /// maskPng:     byte[] PNG mask đen/trắng cùng kích thước
        /// prompt:      mô tả nền muốn fill (để trống = "seamless background")
        /// Trả về byte[] PNG kết quả, hoặc null nếu lỗi.
        /// </summary>
        public static async Task<byte[]> InpaintAsync(byte[] originalPng, byte[] maskPng,
            string prompt = "")
        {
            if (!ApiConfig.IsStabilityConfigured())
                throw new InvalidOperationException("Chưa cấu hình Stability AI API key.");

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(90);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", ApiConfig.StabilityApiKey);

            var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(originalPng), "init_image", "original.png");
            content.Add(new ByteArrayContent(maskPng), "mask_image", "mask.png");

            // mask_source: MASK_IMAGE = dùng mask_image để xác định vùng inpaint
            content.Add(new StringContent("MASK_IMAGE"), "mask_source");

            string fillPrompt = string.IsNullOrWhiteSpace(prompt) ? "seamless background" : prompt;
            content.Add(new StringContent(fillPrompt), "text_prompts[0][text]");
            content.Add(new StringContent("1.0"), "text_prompts[0][weight]");
            content.Add(new StringContent(ApiConfig.InpaintingSteps.ToString()), "steps");
            content.Add(new StringContent("7"), "cfg_scale");
            content.Add(new StringContent("1"), "samples");

            HttpResponseMessage response = await client.PostAsync(ApiConfig.StabilityInpaintingUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[InpaintingClient] Lỗi {response.StatusCode}: {error}");
                return null;
            }

            string resultJson = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject<dynamic>(resultJson);
            string base64 = (string)result.artifacts[0].base64;
            return Convert.FromBase64String(base64);
        }

        // ── Utility: Tạo mask PNG ────────────────────────────────

        /// <summary>
        /// Tạo mask PNG: vùng selectedArea = TRẮNG (AI fill), còn lại = ĐEN (giữ nguyên).
        /// Dùng cho Magic Eraser.
        /// </summary>
        public static byte[] CreateMaskForErase(int canvasWidth, int canvasHeight,
            Rectangle selectedArea)
        {
            using var mask = new Bitmap(canvasWidth, canvasHeight);
            using var g = Graphics.FromImage(mask);
            g.Clear(Color.Black);                         // toàn canvas đen
            g.FillRectangle(Brushes.White, selectedArea); // vùng chọn trắng
            return BitmapToPng(mask);
        }

        /// <summary>
        /// Tạo mask PNG: vùng NGOÀI nét vẽ = TRẮNG (AI fill), nét vẽ = ĐEN (giữ nguyên).
        /// Dùng cho Auto-Complete: giữ nét đã vẽ, AI fill vùng còn trống.
        /// </summary>
        public static byte[] CreateMaskForAutoComplete(Bitmap canvas, Rectangle workArea)
        {
            // Lấy vùng canvas cần complete
            using var region = canvas.Clone(workArea, canvas.PixelFormat);
            using var mask = new Bitmap(region.Width, region.Height);
            using var g = Graphics.FromImage(mask);
            g.Clear(Color.White);  // Mặc định: toàn vùng trắng (AI fill hết)

            // Nét vẽ tối (không phải màu nền) → ĐEN (giữ nguyên)
            Color bg = canvas.GetPixel(0, 0); // giả sử góc trái trên là màu nền
            for (int x = 0; x < region.Width; x++)
            {
                for (int y = 0; y < region.Height; y++)
                {
                    Color pixel = region.GetPixel(x, y);
                    // Nếu pixel khác màu nền đáng kể → đây là nét vẽ → tô đen trong mask
                    int diff = Math.Abs(pixel.R - bg.R) + Math.Abs(pixel.G - bg.G) + Math.Abs(pixel.B - bg.B);
                    if (diff > 30)
                        mask.SetPixel(x, y, Color.Black);
                }
            }
            return BitmapToPng(mask);
        }

        /// <summary>Chuyển Bitmap thành byte[] PNG.</summary>
        public static byte[] BitmapToPng(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        /// <summary>Chuyển Bitmap thành byte[] JPEG (nén 70%).</summary>
        public static byte[] BitmapToJpeg(Bitmap bmp, long quality = 70L)
        {
            using var ms = new MemoryStream();
            var encoder = GetJpegEncoder();
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            bmp.Save(ms, encoder, encoderParams);
            return ms.ToArray();
        }

        private static ImageCodecInfo GetJpegEncoder()
        {
            foreach (var codec in ImageCodecInfo.GetImageEncoders())
                if (codec.MimeType == "image/jpeg") return codec;
            return null;
        }
    }
}
