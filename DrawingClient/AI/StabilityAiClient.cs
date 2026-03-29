// ============================================================
// DrawingClient/AI/StabilityAiClient.cs
// Tuần 5 — Text-to-Drawing: gọi Stability AI REST API
// Tuần 6 — Dùng lại class này cho Magic Eraser + Auto-Complete (inpainting)
// ============================================================
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SharedLib.AI;
using SharedLib.Logging;

namespace DrawingClient.AI
{
    public static class StabilityAiClient
    {
        // ── Tuần 5: Text-to-Drawing ─────────────────────────────

        /// <summary>
        /// Gửi prompt → nhận PNG byte[] từ Stability AI.
        /// Trả về null nếu API thất bại.
        /// </summary>
        public static async Task<byte[]> GenerateImageAsync(string prompt)
        {
            if (!ApiConfig.IsStabilityConfigured())
                throw new InvalidOperationException("Chưa cấu hình Stability AI API key trong ApiConfig.cs");

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", ApiConfig.StabilityApiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var requestBody = new
            {
                text_prompts = new[] { new { text = prompt, weight = 1.0 } },
                cfg_scale = 7,
                width = ApiConfig.TextToImageWidth,
                height = ApiConfig.TextToImageHeight,
                samples = 1,
                steps = ApiConfig.TextToImageSteps
            };

            string json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(ApiConfig.StabilityTextToImageUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                Logger.Error("StabilityAI", $"Lỗi {response.StatusCode}: {error}");
                return null;
            }

            string resultJson = await response.Content.ReadAsStringAsync();
            
            dynamic result = null;
            try
            {
                result = JsonConvert.DeserializeObject<dynamic>(resultJson);
                if (result == null)
                    throw new InvalidOperationException("JSON deserialize trả về null");
            }
            catch (Exception jsonEx)
            {
                Logger.Exception("StabilityAI", jsonEx);
                throw new InvalidOperationException("Không thể parse response JSON từ Stability AI", jsonEx);
            }

            // Validate artifacts array
            if (result.artifacts == null || result.artifacts.Count == 0)
                throw new InvalidOperationException("Response không chứa artifacts");

            string base64 = null;
            try
            {
                base64 = (string)result.artifacts[0].base64;
                if (string.IsNullOrEmpty(base64))
                    throw new InvalidOperationException("base64 string trống");
            }
            catch (Exception ex)
            {
                Logger.Exception("StabilityAI", ex);
                throw;
            }

            // Convert base64
            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException fmtEx)
            {
                Logger.Exception("StabilityAI", fmtEx);
                throw new InvalidOperationException("base64 data không hợp lệ", fmtEx);
            }
        }
    }
}
