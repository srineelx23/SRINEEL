namespace VIMS.Domain.DTOs
{
    public class BusinessRuleViolationDto
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
    }
}