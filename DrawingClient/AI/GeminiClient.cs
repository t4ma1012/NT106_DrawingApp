using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedLib.AI;
using SharedLib.Logging;

namespace DrawingClient.AI
{
    public static class GeminiClient
    {
        private static readonly SemaphoreSlim RequestGate = new SemaphoreSlim(2, 2);

        public static async Task<byte[]> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (!ApiConfig.IsGeminiConfigured())
                throw new InvalidOperationException("Chua cau hinh GEMINI_API_KEY trong .env.");

            string model = ApiConfig.GeminiImageModel;
            string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={ApiConfig.GeminiApiKey}";

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7
                }
            };

            string json = JsonConvert.SerializeObject(body);
            await RequestGate.WaitAsync(cancellationToken);
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
                using var response = await client.PostAsync(
                    endpoint,
                    new StringContent(json, Encoding.UTF8, "application/json"),
                    cancellationToken);

                string responseText = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string shortBody = responseText.Length > 600 ? responseText.Substring(0, 600) : responseText;
                    Logger.Error("Gemini", $"HTTP {(int)response.StatusCode}: {shortBody}");
                    throw new InvalidOperationException("Gemini API tra ve loi. Kiem tra key, model, va quota.");
                }

                JObject root = JObject.Parse(responseText);
                string base64 = FindInlineImageData(root);
                if (string.IsNullOrWhiteSpace(base64))
                    throw new InvalidOperationException("Gemini khong tra ve du lieu anh.");

                return Convert.FromBase64String(base64);
            }
            catch (FormatException ex)
            {
                Logger.Exception("Gemini", ex);
                throw new InvalidOperationException("Du lieu anh tu Gemini khong hop le.", ex);
            }
            finally
            {
                RequestGate.Release();
            }
        }

        private static string FindInlineImageData(JToken root)
        {
            var candidates = root?["candidates"] as JArray;
            if (candidates == null)
                return null;

            foreach (JToken candidate in candidates)
            {
                var parts = candidate["content"]?["parts"] as JArray;
                if (parts == null)
                    continue;

                foreach (JToken part in parts)
                {
                    string data =
                        part["inlineData"]?["data"]?.ToString() ??
                        part["inline_data"]?["data"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(data))
                        return data;
                }
            }

            return null;
        }
    }
}
