namespace VIMS.Domain.DTOs
{
    public class ContextDataDto
    {
        public string DbJson { get; set; } = "{}";
        public string RulesText { get; set; } = string.Empty;
        public List<string> DataUsed { get; set; } = new();
        public List<string> RulesUsed { get; set; } = new();
    }
}
