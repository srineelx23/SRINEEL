namespace VIMS.Domain.DTOs
{
    public class PaymentContextDto
    {
        public int PaymentId { get; set; }
        public int PolicyId { get; set; }
        public string PolicyNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string? TransactionReference { get; set; }
    }
}