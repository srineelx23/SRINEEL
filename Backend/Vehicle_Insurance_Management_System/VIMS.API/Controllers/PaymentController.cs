using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using System;
using System.Linq;

namespace VIMS.API.Controllers
{
    [Authorize(Roles = "Customer")]
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IRazorpayService _razorpayService;
        private readonly IPolicyRepository _policyRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly ICustomerService _customerService;
        private readonly IReferralService _referralService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IRazorpayService razorpayService,
            IPolicyRepository policyRepository,
            IPaymentRepository paymentRepository,
            ICustomerService customerService,
            IReferralService referralService,
            IConfiguration configuration,
            ILogger<PaymentController> logger)
        {
            _razorpayService = razorpayService;
            _policyRepository = policyRepository;
            _paymentRepository = paymentRepository;
            _customerService = customerService;
            _referralService = referralService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("create-order/{policyId}")]
        public async Task<IActionResult> CreateOrder(int policyId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var policy = await _policyRepository.GetByIdAsync(policyId);

                if (policy == null || policy.CustomerId != userId)
                    return NotFound(new { message = "Policy not found" });

                var keyId = _configuration["Razorpay:KeyId"];
                var keySecret = _configuration["Razorpay:KeySecret"];
                if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
                {
                    _logger.LogError("Razorpay keys are missing from configuration.");
                    return StatusCode(500, new { message = "Payment gateway is not configured." });
                }

                // Detect if this is a transfer fee pending payment
                var isTransfer = policy.Vehicle?.VehicleApplication?.IsTransfer == true;
                decimal baseAmount = policy.PremiumAmount;
                decimal discountAmount = 0;

                if (isTransfer && policy.Status == PolicyStatus.PendingPayment)
                {
                    var payments = await _paymentRepository.GetByPolicyIdAsync(policyId);
                    if (payments == null || !Enumerable.Any(payments)) baseAmount = 500;
                }

                var finalAmount = baseAmount;
                if (!(isTransfer && baseAmount == 500))
                {
                    var discountPreview = await _referralService.GetDiscountPreviewAsync(userId, policyId, baseAmount);
                    discountAmount = discountPreview.DiscountAmount;
                    finalAmount = discountPreview.FinalAmount;
                }

                var receipt = $"rcpt_{Guid.NewGuid().ToString("N").Substring(0, 10)}";
                var order = await _razorpayService.CreateOrderAsync(finalAmount, receipt, $"Premium payment for {policy.PolicyNumber}");
                if (string.IsNullOrWhiteSpace(order.OrderId))
                {
                    _logger.LogError("Razorpay returned an empty order id. PolicyId: {PolicyId}, UserId: {UserId}", policyId, userId);
                    return StatusCode(502, new { message = "Failed to create payment order." });
                }

                _logger.LogInformation(
                    "Razorpay order created. PolicyId: {PolicyId}, UserId: {UserId}, OrderId: {OrderId}, AmountPaise: {AmountPaise}, Currency: {Currency}",
                    policyId,
                    userId,
                    order.OrderId,
                    order.AmountPaise,
                    order.Currency);

                return Ok(new
                {
                    orderId = order.OrderId,
                    keyId,
                    amount = order.AmountPaise,
                    currency = order.Currency,
                    policyNumber = policy.PolicyNumber,
                    baseAmount,
                    discountAmount,
                    finalAmount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateOrder failed for PolicyId: {PolicyId}", policyId);
                return StatusCode(500, new { message = "Unable to create payment order right now." });
            }
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyPayment([FromBody] PaymentVerificationModel model)
        {
            try
            {
                if (model == null ||
                    model.PolicyId <= 0 ||
                    string.IsNullOrWhiteSpace(model.RazorpayOrderId) ||
                    string.IsNullOrWhiteSpace(model.RazorpayPaymentId) ||
                    string.IsNullOrWhiteSpace(model.RazorpaySignature))
                {
                    return BadRequest(new { message = "Invalid payment verification payload." });
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                _logger.LogInformation(
                    "Verify request received. PolicyId: {PolicyId}, UserId: {UserId}, OrderId: {OrderId}, PaymentId: {PaymentId}",
                    model.PolicyId,
                    userId,
                    model.RazorpayOrderId,
                    model.RazorpayPaymentId);

                bool isValid = _razorpayService.VerifyPayment(model.RazorpayPaymentId, model.RazorpayOrderId, model.RazorpaySignature);

                if (!isValid)
                {
                    _logger.LogWarning(
                        "Razorpay signature verification failed. PolicyId: {PolicyId}, UserId: {UserId}, OrderId: {OrderId}, PaymentId: {PaymentId}",
                        model.PolicyId,
                        userId,
                        model.RazorpayOrderId,
                        model.RazorpayPaymentId);
                    return BadRequest(new { message = "Payment verification failed" });
                }

                var existingPayments = await _paymentRepository.GetByPolicyIdAsync(model.PolicyId);
                if (existingPayments.Any(p => p.TransactionReference == model.RazorpayPaymentId && p.Status == PaymentStatus.Paid))
                {
                    _logger.LogInformation("Duplicate verify callback ignored for payment id {PaymentId}", model.RazorpayPaymentId);
                    return Ok(new { message = "Payment already processed." });
                }

                var policy = await _policyRepository.GetByIdAsync(model.PolicyId);
                if (policy == null || policy.CustomerId != userId)
                {
                    return NotFound(new { message = "Policy not found" });
                }

                var isTransfer = policy.Vehicle?.VehicleApplication?.IsTransfer == true;
                decimal baseAmount = policy.PremiumAmount;
                if (isTransfer && policy.Status == PolicyStatus.PendingPayment)
                {
                    var policyPayments = await _paymentRepository.GetByPolicyIdAsync(model.PolicyId);
                    if (policyPayments == null || !Enumerable.Any(policyPayments))
                    {
                        baseAmount = 500;
                    }
                }

                decimal discountAmount = 0;
                decimal finalAmount = baseAmount;
                if (!(isTransfer && baseAmount == 500))
                {
                    var discountPreview = await _referralService.GetDiscountPreviewAsync(userId, model.PolicyId, baseAmount);
                    discountAmount = discountPreview.DiscountAmount;
                    finalAmount = discountPreview.FinalAmount;
                }

                // Once verified, proceed with existing policy update logic
                var result = await _customerService.PayAnnualPremiumAsync(
                    model.PolicyId,
                    userId,
                    finalAmount,
                    PaymentMethod.Online,
                    model.RazorpayPaymentId);

                if (discountAmount > 0)
                {
                    await _referralService.ProcessRewardAfterPaymentAsync(userId, model.PolicyId, discountAmount);
                }

                _logger.LogInformation(
                    "Payment verification succeeded. PolicyId: {PolicyId}, UserId: {UserId}, OrderId: {OrderId}, PaymentId: {PaymentId}",
                    model.PolicyId,
                    userId,
                    model.RazorpayOrderId,
                    model.RazorpayPaymentId);

                return Ok(new { message = result, discountAmount, finalAmount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyPayment failed for PolicyId: {PolicyId}", model?.PolicyId);
                return StatusCode(500, new { message = "Unable to verify payment right now." });
            }
        }
    }

    public class PaymentVerificationModel
    {
        public int PolicyId { get; set; }
        public string RazorpayOrderId { get; set; } = "";
        public string RazorpayPaymentId { get; set; } = "";
        public string RazorpaySignature { get; set; } = "";
    }
}
