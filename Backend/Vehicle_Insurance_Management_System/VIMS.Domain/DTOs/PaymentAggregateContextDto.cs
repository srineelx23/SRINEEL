namespace VIMS.Domain.DTOs
{
    public class PaymentAggregateContextDto
    {
        public decimal TotalPaidAmount { get; set; }
        public decimal TotalAmountAllStatuses { get; set; }
        public int PaidPaymentsCount { get; set; }
        public int TotalPaymentsCount { get; set; }
        public decimal TotalClaimPayoutAmount { get; set; }
        public int ClaimPayoutCount { get; set; }
        public decimal TotalTransferFeeAmount { get; set; }
        public int TransferFeeCount { get; set; }
    }
}