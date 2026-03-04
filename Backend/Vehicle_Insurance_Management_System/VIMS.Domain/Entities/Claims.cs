using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Enums;

namespace VIMS.Domain.Entities
{
    public class Claims
    {
        [Key]
        public int ClaimId { get; set; }
        // Business Identifier (Formatted like CLM-2026-000045)
        public string ClaimNumber { get; set; } = string.Empty;
        public int PolicyId { get; set; }
        public int CustomerId { get; set; }
        public int? ClaimsOfficerId { get; set; }
        public ClaimType claimType { get; set; }
        public ClaimStatus Status { get; set; }
        public decimal? ApprovedAmount { get; set; }
        // Computed classification after decision: Partial | TotalLoss | ConstructiveTotalLoss
        public string? DecisionType { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // Navigation
        public Policy Policy { get; set; } = null!;
        public User Customer { get; set; } = null!;
        public User? ClaimsOfficer { get; set; }
        public ICollection<ClaimDocument>? Documents { get; set; }
    }
}
