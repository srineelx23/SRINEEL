namespace VIMS.Application.Settings
{
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ChatModel { get; set; } = "gemini-2.0-flash";
        public string EmbeddingModel { get; set; } = "models/text-embedding-004";
        public int TopK { get; set; } = 5;
        public int ChunkSize { get; set; } = 900;
        public float MinimumSimilarity { get; set; } = 0.45f;
    }
}
