using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Enums;

namespace VIMS.Domain.Entities
{
    public class Policy
    {
        [Key]
        public int PolicyId { get; set; }
        // Business Identifier (Formatted like POL-2026-000123)
        public string PolicyNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public int? AgentId { get; set; }
        public int VehicleId { get; set; }
        public int PlanId { get; set; }
        public PolicyStatus Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal PremiumAmount { get; set; }
        public decimal InvoiceAmount { get; set; }
        public decimal IDV { get; set; }
        public int SelectedYears { get; set; }
        public int CurrentYearNumber { get; set; }
        public DateTime CurrentYearEndDate { get; set; }
        public bool IsCurrentYearPaid { get; set; }
        public DateTime? CancellationDate { get; set; }
        public bool IsRenewed { get; set; }
        public int InitialKilometersDriven { get; set; }
        public int ClaimCount { get; set; }
        // Navigation
        public User Customer { get; set; } = null!;
        public User? Agent { get; set; }
        public Vehicle Vehicle { get; set; } = null!;
        public PolicyPlan Plan { get; set; } = null!;
        public ICollection<Payment>? Payments { get; set; }
        public ICollection<Claims>? Claims { get; set; }
    }
}
