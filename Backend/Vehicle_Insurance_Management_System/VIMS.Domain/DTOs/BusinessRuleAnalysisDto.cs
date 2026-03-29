namespace VIMS.Domain.DTOs
{
    public class BusinessRuleAnalysisDto
    {
        public List<BusinessRuleViolationDto> Violations { get; set; } = new();
        public bool Eligibility { get; set; } = true;
        public Dictionary<string, object> ComputedValues { get; set; } = new();
    }
}