namespace VIMS.Domain.DTOs
{
    public class ReferralContextDto
    {
        public int ReferralId { get; set; }
        public int ReferrerUserId { get; set; }
        public int RefereeUserId { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal RewardAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
