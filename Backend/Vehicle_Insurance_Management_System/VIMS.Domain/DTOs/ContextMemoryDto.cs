namespace VIMS.Domain.DTOs
{
    public class ContextMemoryDto
    {
        public string LastIntent { get; set; } = "GENERAL";
        public string LastEntityId { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int? AnchorPolicyId { get; set; }
        public int? AnchorVehicleId { get; set; }
        public string LastPlanType { get; set; } = "NONE";
        public int? LastRangeDays { get; set; }
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}