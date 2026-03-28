namespace VIMS.Domain.DTOs
{
    public class PaymentAggregateContextDto
    {
        public decimal TotalPaidAmount { get; set; }
        public decimal TotalAmountAllStatuses { get; set; }
        public int PaidPaymentsCount { get; set; }
        public int TotalPaymentsCount { get; set; }
    }
}