// ============================================================
// SharedLib/AI/ApiConfig.cs
// Tuần 5-6 — Quản lý API key cho tính năng AI
// B là người DUY NHẤT quản lý file này. KHÔNG commit lên GitHub public!
// ============================================================

namespace SharedLib.AI
{
    /// <summary>
    /// API keys cho các tính năng AI:
    /// - Stability AI: Text-to-Drawing, Magic Eraser, Auto-Complete
    /// - Remove.bg: Background Remover
    /// Voice-to-Draw dùng System.Speech (.NET built-in) → KHÔNG cần key.
    /// </summary>
    public static class ApiConfig
    {
        // ── Stability AI ────────────────────────────────────────
        // Đăng ký tại: https://platform.stability.ai → API Keys
        // Free tier: 25 credits/ngày (đủ để demo)
        public static string StabilityApiKey { get; set; } = "YOUR_STABILITY_AI_KEY_HERE";

        // Endpoint Text-to-Image
        public const string StabilityTextToImageUrl =
            "https://api.stability.ai/v1/generation/stable-diffusion-v1-6/text-to-image";

        // Endpoint Inpainting (Magic Eraser + Auto-Complete)
        public const string StabilityInpaintingUrl =
            "https://api.stability.ai/v1/generation/stable-inpainting-512-v2-0/image-to-image/masking";

        // ── Remove.bg ───────────────────────────────────────────
        // Đăng ký tại: https://www.remove.bg → Dashboard → API Key
        // Free tier: 50 ảnh/tháng
        public static string RemoveBgApiKey { get; set; } = "YOUR_REMOVE_BG_KEY_HERE";

        public const string RemoveBgUrl = "https://api.remove.bg/v1.0/removebg";

        // ── Thông số AI ──────────────────────────────────────────
        public const int TextToImageWidth = 512;
        public const int TextToImageHeight = 512;
        public const int TextToImageSteps = 30;
        public const int InpaintingSteps = 30;

        /// <summary>Kiểm tra API key đã được cấu hình chưa.</summary>
        public static bool IsStabilityConfigured()
            => !string.IsNullOrWhiteSpace(StabilityApiKey)
               && StabilityApiKey != "YOUR_STABILITY_AI_KEY_HERE";

        public static bool IsRemoveBgConfigured()
            => !string.IsNullOrWhiteSpace(RemoveBgApiKey)
               && RemoveBgApiKey != "YOUR_REMOVE_BG_KEY_HERE";
    }
}
