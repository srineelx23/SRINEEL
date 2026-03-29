namespace VIMS.Application.DTOs
{
    public class RagChunkDTO
    {
        public string Type { get; set; } = "GENERAL";
        public string Source { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        // Backward-compatible aliases used by existing code paths.
        public string SourceType { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public float Similarity { get; set; }
    }
}
