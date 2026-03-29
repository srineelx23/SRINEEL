namespace VIMS.Domain.DTOs
{
    public class IntentResultDto
    {
        public string IntentType { get; set; } = "GENERAL";
        public string QueryPlanHint { get; set; } = "GENERAL";
        public ContextMemoryDto ContextMemory { get; set; } = new();
        public int? ClaimId { get; set; }
        public int? UserId { get; set; }
        public int? PolicyId { get; set; }
        public int? RequestedRangeDays { get; set; }
        public string VehicleTypeFilter { get; set; } = string.Empty;
        public string FuelTypeFilter { get; set; } = string.Empty;
        public string PlanTypeFilter { get; set; } = string.Empty;
        public bool RequiresExplanation { get; set; }
        public bool IsFollowUp { get; set; }
        public bool IncludeClaims { get; set; }
        public bool IncludeUsers { get; set; }
        public bool IncludePolicies { get; set; }
        public bool IncludeReferrals { get; set; }
        public bool IncludeVehicles { get; set; }
        public bool IncludePayments { get; set; }
        public bool IncludeApplications { get; set; }
        public bool IncludeGarages { get; set; }
        public bool IncludeNotifications { get; set; }
    }
}
