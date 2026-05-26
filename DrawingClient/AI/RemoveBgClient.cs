using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SharedLib.AI;

namespace DrawingClient.AI
{
    public static class RemoveBgClient
    {
        public const int MaxInputBytes = 22 * 1024 * 1024;

        public static async Task<byte[]> RemoveBackgroundAsync(byte[] inputImageBytes, CancellationToken cancellationToken = default)
        {
            if (!ApiConfig.IsRemoveBgConfigured())
                throw new InvalidOperationException("Chua cau hinh REMOVE_BG_API_KEY trong .env.");

            if (inputImageBytes == null || inputImageBytes.Length == 0)
                throw new InvalidOperationException("Anh dau vao rong.");

            if (inputImageBytes.Length > MaxInputBytes)
                throw new InvalidOperationException("Anh dau vao vuot qua gioi han 22 MB cua Remove.bg.");

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("X-Api-Key", ApiConfig.RemoveBgApiKey);

            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(inputImageBytes), "image_file", "input.png");
            content.Add(new StringContent("auto"), "size");
            content.Add(new StringContent("png"), "format");

            try
            {
                // KET NOI DU LIEU/NETWORK: goi Remove.bg bang HTTP async de UI khong bi khoa.
                using var response = await client.PostAsync(ApiConfig.RemoveBgUrl, content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    // XU LY BAT DONG BO: doc noi dung loi tu response stream bang async.
                    string error = await response.Content.ReadAsStringAsync();
                    throw BuildRemoveBgException(response.StatusCode, error);
                }

                // XU LY BAT DONG BO: doc bytes anh ket qua tu network stream.
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Remove.bg phan hoi qua lau. Hay thu lai sau.", ex);
            }
        }

        public static async Task<Bitmap> RemoveBackgroundAsync(Bitmap source, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            source.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] inputBytes = ms.ToArray();

            // XU LY BAT DONG BO: tai su dung ham async dang byte[] cho luong Bitmap.
            byte[] resultBytes = await RemoveBackgroundAsync(inputBytes, cancellationToken);

            using var resultMs = new MemoryStream(resultBytes);
            return new Bitmap(resultMs);
        }

        private static Exception BuildRemoveBgException(System.Net.HttpStatusCode statusCode, string responseText)
        {
            int code = (int)statusCode;
            string message = TryReadErrorMessage(responseText);

            if (code == 401 || code == 403)
                return new InvalidOperationException("REMOVE_BG_API_KEY khong hop le hoac khong co quyen.");

            if (code == 402 || code == 429)
                return new InvalidOperationException("Remove.bg het credit/quota hoac bi gioi han toc do.");

            if (code >= 500)
                return new InvalidOperationException("Remove.bg dang loi tam thoi. Hay thu lai sau.");

            if (!string.IsNullOrWhiteSpace(message))
                return new InvalidOperationException("Remove.bg tra ve loi: " + message);

            return new InvalidOperationException("Remove.bg khong xu ly duoc anh nay.");
        }

        private static string TryReadErrorMessage(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return string.Empty;

            try
            {
                JObject root = JObject.Parse(responseText);
                return root["errors"]?[0]?["title"]?.ToString()
                    ?? root["errors"]?[0]?["detail"]?.ToString()
                    ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
