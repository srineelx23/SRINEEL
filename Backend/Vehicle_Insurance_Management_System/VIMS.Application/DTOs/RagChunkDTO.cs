namespace VIMS.Application.DTOs
{
    public class RagChunkDTO
    {
        public string SourceType { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public float Similarity { get; set; }
    }
}
