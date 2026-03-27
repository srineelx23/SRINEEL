namespace VIMS.Application.DTOs
{
    public class RuleDefinition
    {
        public string Id { get; set; } = string.Empty;
        public List<string> Keywords { get; set; } = new();
        public string Response { get; set; } = string.Empty;
        public int Priority { get; set; }
    }
}
