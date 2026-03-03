using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Enums;

namespace VIMS.Domain.Entities
{
    public class VehicleApplication
    {
        [Key]
        public int VehicleApplicationId { get; set; }
        public int CustomerId { get; set; }
        public User Customer { get; set; }
        public int? AssignedAgentId { get; set; }
        public User? AssignedAgent { get; set; }
        public int PlanId { get; set; }
        public PolicyPlan? Plan { get; set; }
        public string RegistrationNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public string FuelType { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public int KilometersDriven { get; set; }
        public int PolicyYears { get; set; }
        public decimal InvoiceAmount { get; set; }
        public VehicleApplicationStatus Status { get; set; }
        public string? RejectionReason { get; set; }
        public bool IsTransfer { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<VehicleDocument> Documents { get; set; } = new List<VehicleDocument>();
    }
}
