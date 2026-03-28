namespace VIMS.Domain.DTOs
{
    public class ReferralAbuseSignalDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int TotalReferrals { get; set; }
        public int CompletedReferrals { get; set; }
        public int PendingReferrals { get; set; }
        public decimal TotalDiscountGiven { get; set; }
        public decimal TotalRewardEarned { get; set; }
        public string RiskLevel { get; set; } = "LOW";
        public List<string> Signals { get; set; } = new();
    }
}
