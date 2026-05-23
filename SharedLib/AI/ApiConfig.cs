using SharedLib.Config;

namespace SharedLib.AI
{
    public static class ApiConfig
    {
        public static string GeminiApiKey => EnvLoader.Get("GEMINI_API_KEY", "");
        public static string GeminiImageModel => EnvLoader.Get("GEMINI_IMAGE_MODEL", "gemini-2.5-flash-image-preview");
        public static string GeminiTextModel => EnvLoader.Get("GEMINI_TEXT_MODEL", "gemini-2.5-flash");

        public static string RemoveBgApiKey => EnvLoader.Get("REMOVE_BG_API_KEY", "");
        public const string RemoveBgUrl = "https://api.remove.bg/v1.0/removebg";

        public const int TextToImageWidth = 512;
        public const int TextToImageHeight = 512;
        public const int TextToImageSteps = 30;

        public static bool IsGeminiConfigured()
            => !string.IsNullOrWhiteSpace(GeminiApiKey);

        public static bool IsRemoveBgConfigured()
            => !string.IsNullOrWhiteSpace(RemoveBgApiKey);
    }
}
