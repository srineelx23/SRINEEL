namespace VIMS.Domain.DTOs
{
    public class PolicyContextDto
    {
        public int PolicyId { get; set; }
        public string PolicyNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public int VehicleId { get; set; }
        public int PlanId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal PremiumAmount { get; set; }
        public decimal InvoiceAmount { get; set; }
        public decimal IDV { get; set; }
        public int ClaimCount { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string VehicleRegistrationNumber { get; set; } = string.Empty;
        public string VehicleMake { get; set; } = string.Empty;
        public string VehicleModel { get; set; } = string.Empty;
        public int VehicleYear { get; set; }
    }
}
