using VIMS.Domain.Enums;

namespace VIMS.Domain.Entities
{
    public class Referral
    {
        public int ReferralId { get; set; }
        public int ReferrerUserId { get; set; }
        public int RefereeUserId { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal RewardAmount { get; set; }
        public ReferralStatus Status { get; set; } = ReferralStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public User ReferrerUser { get; set; } = null!;
        public User RefereeUser { get; set; } = null!;
    }
}
