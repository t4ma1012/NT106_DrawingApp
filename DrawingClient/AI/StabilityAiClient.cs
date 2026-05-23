using System.Threading.Tasks;

namespace DrawingClient.AI
{
    public static class StabilityAiClient
    {
        // Backward-compatible wrapper: current implementation uses Gemini.
        public static Task<byte[]> GenerateImageAsync(string prompt)
        {
            return GeminiClient.GenerateImageAsync(prompt);
        }
    }
}
