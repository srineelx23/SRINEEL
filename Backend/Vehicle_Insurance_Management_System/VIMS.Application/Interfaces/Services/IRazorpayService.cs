using System.Threading.Tasks;

namespace VIMS.Application.Interfaces.Services
{
    public class RazorpayOrderResult
    {
        public string OrderId { get; set; } = string.Empty;
        public long AmountPaise { get; set; }
        public string Currency { get; set; } = "INR";
    }

    public interface IRazorpayService
    {
        Task<RazorpayOrderResult> CreateOrderAsync(decimal amount, string receipt, string notes);
        bool VerifyPayment(string paymentId, string orderId, string signature);
    }
}
