namespace VIMS.Domain.DTOs
{
    public class VehicleApplicationContextDto
    {
        public int VehicleApplicationId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int? AssignedAgentId { get; set; }
        public string? AssignedAgentName { get; set; }
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string RegistrationNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public string FuelType { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public int KilometersDriven { get; set; }
        public int PolicyYears { get; set; }
        public decimal InvoiceAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public bool IsTransfer { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}