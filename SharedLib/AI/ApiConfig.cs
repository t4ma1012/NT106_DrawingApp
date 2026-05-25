using SharedLib.Config;

namespace SharedLib.AI
{
    public static class ApiConfig
    {
        public static string HuggingFaceToken
        {
            get
            {
                string token = EnvLoader.Get("HF_TOKEN", "");
                return string.IsNullOrWhiteSpace(token)
                    ? EnvLoader.Get("HUGGINGFACE_API_TOKEN", "")
                    : token;
            }
        }
        public static string HuggingFaceImageModel => EnvLoader.Get("HF_IMAGE_MODEL", "stabilityai/stable-diffusion-xl-base-1.0");

        public static string RemoveBgApiKey => EnvLoader.Get("REMOVE_BG_API_KEY", "");
        public const string RemoveBgUrl = "https://api.remove.bg/v1.0/removebg";
        public const string HuggingFaceImageGenerationUrl = "https://router.huggingface.co/nscale/v1/images/generations";

        public const int TextToImageWidth = 512;
        public const int TextToImageHeight = 512;
        public const int TextToImageSteps = 30;

        public static bool IsHuggingFaceConfigured()
            => !string.IsNullOrWhiteSpace(HuggingFaceToken);

        public static bool IsRemoveBgConfigured()
            => !string.IsNullOrWhiteSpace(RemoveBgApiKey);
    }
}
