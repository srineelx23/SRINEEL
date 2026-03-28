namespace VIMS.Domain.DTOs
{
    public class LlmResponseDto
    {
        public string Answer { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
        public List<string> RulesApplied { get; set; } = new();
        public List<string> DataUsed { get; set; } = new();
        public string Confidence { get; set; } = "LOW";
    }
}
