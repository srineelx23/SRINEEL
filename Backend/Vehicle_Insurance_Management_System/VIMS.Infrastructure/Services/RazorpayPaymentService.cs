using Razorpay.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VIMS.Application.Interfaces.Services;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace VIMS.Infrastructure.Services
{
    public class RazorpayPaymentService : IRazorpayService
    {
        private readonly string _keyId;
        private readonly string _keySecret;
        private readonly ILogger<RazorpayPaymentService> _logger;

        public RazorpayPaymentService(IConfiguration configuration, ILogger<RazorpayPaymentService> logger)
        {
            _logger = logger;
            _keyId = configuration["Razorpay:KeyId"] ?? "";
            _keySecret = configuration["Razorpay:KeySecret"] ?? "";

            if (string.IsNullOrWhiteSpace(_keyId) || string.IsNullOrWhiteSpace(_keySecret))
                throw new InvalidOperationException("Razorpay keys are not configured.");

            if (!_keyId.StartsWith("rzp_", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Razorpay KeyId format is invalid.");
        }

        public Task<RazorpayOrderResult> CreateOrderAsync(decimal amount, string receipt, string notes)
        {
            var client = new RazorpayClient(_keyId, _keySecret);
            var amountPaise = Math.Max(100, (int)Math.Round(amount * 100M, MidpointRounding.AwayFromZero));

            Dictionary<string, object> options = new Dictionary<string, object>
            {
                { "amount", amountPaise },
                { "currency", "INR" },
                { "receipt", receipt }
            };

            var order = client.Order.Create(options);
            Console.WriteLine(order.Attributes.ToString());
            var result = new RazorpayOrderResult
            {
                OrderId = order["id"]?.ToString() ?? string.Empty,
                AmountPaise = Convert.ToInt64(order["amount"] ?? amountPaise),
                Currency = order["currency"]?.ToString() ?? "INR"
            };

            return Task.FromResult(result);
        }

        public bool VerifyPayment(string paymentId, string orderId, string signature)
        {
            Dictionary<string, string> attributes = new Dictionary<string, string>
            {
                { "razorpay_payment_id", paymentId },
                { "razorpay_order_id", orderId },
                { "razorpay_signature", signature }
            };

            try
            {
                Utils.verifyPaymentSignature(attributes);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Razorpay signature verification failed for OrderId: {OrderId}, PaymentId: {PaymentId}",
                    orderId,
                    paymentId);
                return false;
            }
        }
    }
}
