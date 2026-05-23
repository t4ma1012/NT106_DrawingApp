using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using SharedLib.AI;

namespace DrawingClient.AI
{
    public static class RemoveBgClient
    {
        public static async Task<byte[]> RemoveBackgroundAsync(byte[] inputImageBytes)
        {
            if (!ApiConfig.IsRemoveBgConfigured())
                throw new InvalidOperationException("Chua cau hinh REMOVE_BG_API_KEY trong .env.");

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("X-Api-Key", ApiConfig.RemoveBgApiKey);

            var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(inputImageBytes), "image_file", "input.png");
            content.Add(new StringContent("auto"), "size");
            content.Add(new StringContent("rgba"), "format");

            using var response = await client.PostAsync(ApiConfig.RemoveBgUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                Console.WriteLine("[RemoveBgClient] Loi " + response.StatusCode + ": " + error);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        public static async Task<Bitmap> RemoveBackgroundAsync(Bitmap source)
        {
            using var ms = new MemoryStream();
            source.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] inputBytes = ms.ToArray();

            byte[] resultBytes = await RemoveBackgroundAsync(inputBytes);
            if (resultBytes == null)
                return null;

            using var resultMs = new MemoryStream(resultBytes);
            return new Bitmap(resultMs);
        }
    }
}
