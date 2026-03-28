namespace VIMS.Domain.DTOs
{
    public class ClaimContextDto
    {
        public int ClaimId { get; set; }
        public string ClaimNumber { get; set; } = string.Empty;
        public int PolicyId { get; set; }
        public int CustomerId { get; set; }
        public int? ClaimsOfficerId { get; set; }
        public string ClaimType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal? ApprovedAmount { get; set; }
        public string? DecisionType { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
