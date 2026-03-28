namespace VIMS.Application.DTOs
{
    public class ReferralDiscountPreviewDTO
    {
        public bool IsEligible { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
