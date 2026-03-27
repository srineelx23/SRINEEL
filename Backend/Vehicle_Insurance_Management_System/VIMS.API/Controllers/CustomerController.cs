using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Services;
using VIMS.Domain.Entities;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace VIMS.API.Controllers
{
    [Authorize(Roles ="Customer")]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly IPolicyPlanService _policyPlanService;
        private readonly IPricingService _pricingService;
        private readonly IPolicyRepository _policyRepository;
        private readonly VIMS.Application.Interfaces.Repositories.IClaimsRepository _claimsRepository;
        private readonly VIMS.Application.Interfaces.Repositories.IPaymentRepository _paymentRepository;
        private readonly IInvoiceService _invoiceService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IGarageRepository _garageRepository;

        public CustomerController(
            ICustomerService customerService, 
            IPolicyPlanService policyPlanService, 
            IPricingService pricingService, 
            IPolicyRepository policyRepository, 
            VIMS.Application.Interfaces.Repositories.IClaimsRepository claimsRepository, 
            VIMS.Application.Interfaces.Repositories.IPaymentRepository paymentRepository, 
            IInvoiceService invoiceService, 
            IHttpClientFactory httpClientFactory,
            IGarageRepository garageRepository)
        {
            _customerService = customerService;
            _policyPlanService = policyPlanService;
            _pricingService = pricingService;
            _policyRepository = policyRepository;
            _claimsRepository = claimsRepository;
            _paymentRepository = paymentRepository;
            _invoiceService = invoiceService;
            _httpClientFactory = httpClientFactory;
            _garageRepository = garageRepository;
        }

        [HttpGet("invoice/download/{paymentId}")]
        public IActionResult DownloadInvoice(int paymentId)
        {
            var pdfBytes = _invoiceService.GenerateInvoicePdf(paymentId);
            if (pdfBytes == null || pdfBytes.Length == 0)
                return NotFound(new { message = "Invoice not found or could not be generated" });

            return File(pdfBytes, "application/pdf", $"Invoice_{paymentId}.pdf");
        }

        [HttpGet("policy/download/{policyId}")]
        public IActionResult DownloadPolicyContract(int policyId)
        {
            var pdfBytes = _invoiceService.GeneratePolicyContractPdf(policyId);
            if (pdfBytes == null || pdfBytes.Length == 0)
                return NotFound(new { message = "Policy contract not available" });

            return File(pdfBytes, "application/pdf", $"Policy_Contract_{policyId}.pdf");
        }

        [HttpGet("claim/download/{claimId}")]
        public IActionResult DownloadClaimReport(int claimId)
        {
            var pdfBytes = _invoiceService.GenerateClaimSettlementPdf(claimId);
            if (pdfBytes == null || pdfBytes.Length == 0)
                return NotFound(new { message = "Settlement report not available for this claim" });

            return File(pdfBytes, "application/pdf", $"Claim_Settlement_{claimId}.pdf");
        }

        [HttpGet("policy/{policyId}")]

        public async Task<IActionResult> GetPolicy(int policyId)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue))
                return Unauthorized(new { message = "UserId claim missing in token" });

            int customerId = int.Parse(userIdValue);
            var policy = await _policyRepository.GetByIdAsync(policyId);
            if (policy == null || policy.CustomerId != customerId)
                return NotFound(new { message = "Policy not found" });

            // Detect if this is a transfer fee pending payment (same logic as GetMyPolicies)
            bool isFeePending = false;
            var isTransfer = policy.Vehicle?.VehicleApplication?.IsTransfer == true;
            if (isTransfer && policy.Status == VIMS.Domain.Enums.PolicyStatus.PendingPayment)
            {
                var payments = await _paymentRepository.GetByPolicyIdAsync(policy.PolicyId);
                isFeePending = payments == null || !payments.Any();
            }

            var result = new
            {
                policyId = policy.PolicyId,
                policyNumber = policy.PolicyNumber,
                status = policy.Status.ToString(),
                premiumAmount = isFeePending ? 500 : policy.PremiumAmount,
                invoiceAmount = policy.InvoiceAmount,
                idv = policy.IDV, // Added missing IDV
                isFeePending,
                startDate = policy.StartDate,
                endDate = policy.EndDate,
                vehicle = policy.Vehicle == null ? null : new { 
                    vehicleId = policy.Vehicle.VehicleId, 
                    make = policy.Vehicle.Make, 
                    model = policy.Vehicle.Model, 
                    year = policy.Vehicle.Year,
                    vehicleType = policy.Vehicle.VehicleType,
                    registrationNumber = policy.Vehicle.RegistrationNumber,
                    documents = policy.Vehicle.VehicleApplication?.Documents?.Select(d => new { documentType = d.DocumentType, filePath = d.FilePath })
                },
                plan = policy.Plan == null ? null : new { planId = policy.Plan.PlanId, planName = policy.Plan.PlanName }
            };

            return Ok(result);
        }

        [HttpGet("claim/{id}")]
        public async Task<IActionResult> GetClaim(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var claim = await _claimsRepository.GetByIdAsync(id);
            if (claim == null || claim.CustomerId != userId)
                return NotFound(new { message = "Claim not found" });

            var result = new
            {
                claim.ClaimId,
                claim.ClaimNumber,
                claim.PolicyId,
                claim.CustomerId,
                ClaimType = claim.claimType.ToString(),
                Status = claim.Status.ToString(),
                ApprovedAmount = claim.ApprovedAmount,
                claim.RejectionReason,
                DecisionType = claim.DecisionType,
                claim.SettlementBreakdownJson,
                claim.CreatedAt,
                Documents = claim.Documents?.Select(d => new { d.Document1, d.Document2 }),
                Policy = claim.Policy == null ? null : new { claim.Policy.PolicyId, claim.Policy.PolicyNumber, claim.Policy.InvoiceAmount }
            };

            return Ok(result);
        }

        [HttpGet("policy/{policyId}/status")]
        public async Task<IActionResult> GetPolicyStatus(int policyId)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue))
                return Unauthorized(new { message = "UserId claim missing in token" });

            int customerId = int.Parse(userIdValue);
            var policy = await _policyRepository.GetByIdAsync(policyId);
            if (policy == null || policy.CustomerId != customerId)
                return NotFound(new { message = "Policy not found" });

            // RenewalDue: very simple rule - if EndDate within 30 days
            if (policy.EndDate <= DateTime.UtcNow)
                return Ok(new { status = "Expired" });

            if ((policy.EndDate - DateTime.UtcNow).TotalDays <= 30)
                return Ok(new { status = "RenewalDue" });

            return Ok(new { status = policy.Status.ToString() });
        }

        [HttpGet("policy/{policyId}/payment-status")]
        public async Task<IActionResult> GetPolicyPaymentStatus(int policyId)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue))
                return Unauthorized(new { message = "UserId claim missing in token" });

            int customerId = int.Parse(userIdValue);
            var policy = await _policyRepository.GetByIdAsync(policyId);
            if (policy == null || policy.CustomerId != customerId)
                return NotFound(new { message = "Policy not found" });

            var hasUnpaid = await _paymentRepository.HasUnpaidAsync(policyId);
            return Ok(new { hasUnpaid = hasUnpaid });
        }
        [HttpGet("policy/{policyId}/years")]
        public async Task<IActionResult> GetPolicyYears(int policyId)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue))
                return Unauthorized(new { message = "UserId claim missing in token" });

            int customerId = int.Parse(userIdValue);
            var policy = await _policyRepository.GetByIdAsync(policyId);
            if (policy == null || policy.CustomerId != customerId)
                return NotFound(new { message = "Policy not found" });

            // Determine number of years and build per-year status
            var totalYears = policy.SelectedYears;
            var now = DateTime.UtcNow;
            var yearStatuses = Enumerable.Range(1, totalYears).Select(i =>
            {
                var yearStart = policy.StartDate.AddYears(i - 1);
                var yearEnd = yearStart.AddYears(1);
                string status;
                if (yearEnd <= now)
                    status = "Paid"; // approximate: ended years considered paid
                else if (yearStart <= now && yearEnd > now)
                    status = policy.IsCurrentYearPaid ? "Paid" : "Pending";
                else
                    status = "Upcoming";

                return new { Year = i, Status = status };
            });

            return Ok(yearStatuses);
        }
        [AllowAnonymous]
        [HttpGet("all-policy-plans")]
        public async Task<IActionResult> ViewAllPoliciesAsync()
        {
            var res= await _customerService.ViewAllPoliciesAsync();
            return Ok(res);
        }
        [HttpPost("vehicle-application")]
        public async Task<IActionResult> CreateApplication(CreateVehicleApplicationDTO dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _customerService.CreateApplicationAsync(dto,userId);
            return Ok("Application submitted.");
        }

        [HttpPost("extract-documents")]
        public async Task<IActionResult> ExtractDocuments(IFormFile rcDocument, IFormFile invoiceDocument)
        {
            var ocrService = HttpContext.RequestServices.GetService(typeof(VIMS.Application.Interfaces.Services.IOcrService)) as VIMS.Application.Interfaces.Services.IOcrService;
            if (ocrService == null) return StatusCode(500, "OCR Service not available");

            if (rcDocument == null || invoiceDocument == null)
                return BadRequest("Both RC and Invoice documents are required for extraction.");

            var result = await ocrService.ExtractVehicleDetailsAsync(rcDocument, invoiceDocument);
            return Ok(result);
        }
        [HttpGet("my-applications")]
        public async Task<IActionResult> GetMyApplications()
{
    var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    var applications = await _customerService.GetMyApplicationsAsync(userId);

    return Ok(applications);
}
        [HttpPost("calculate-quote")]
        public async Task<IActionResult> CalculateQuote(CalculateQuoteDTO dto)
        {
            var plan = await _policyPlanService.GetPolicyPlanAsync(dto.PlanId);

            if (plan == null)
                return BadRequest("Invalid plan");

            int vehicleAge = DateTime.UtcNow.Year - dto.ManufactureYear;
            if (vehicleAge > 15)
                return BadRequest("Cannot buy insurance for vehicles aged greater than 15 years");

            // Pricing service expects the DTO and plan per its interface signature
            var result = _pricingService.CalculateAnnualPremium(dto, plan, false);

            return Ok(result);
        }
        [HttpGet("my-policies")]
        public async Task<IActionResult> GetMyPolicies()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdValue))
                return Unauthorized(new { message = "UserId claim missing in token" });

            int customerId = int.Parse(userIdValue);

            var result = await _customerService.GetMyPoliciesAsync(customerId);

            return Ok(result);
        }
        [HttpPost("pay-annual/{policyId}")]
        public async Task<IActionResult> PayAnnual(int policyId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var result = await _customerService.PayAnnualPremiumAsync(policyId, userId);

            return Ok(result);
        }

        [HttpPost("renew/{policyId}")]
        public async Task<IActionResult> RenewPolicy(int policyId, RenewPolicyDTO dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var result = await _customerService.RenewPolicyAsync(policyId, dto, userId);

            return Ok(result);
        }

        [HttpPost("claim/submit")]
        public async Task<IActionResult> SubmitClaim([FromForm] SubmitClaimDTO dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var claimsService = HttpContext.RequestServices.GetService(typeof(VIMS.Application.Interfaces.Services.IClaimsService)) as VIMS.Application.Interfaces.Services.IClaimsService;
            if (claimsService == null)
                return StatusCode(500, "Claims service not available");

            var res = await claimsService.SubmitClaimAsync(dto, userId);
            return Ok(new { message = res });
        }

        [HttpGet("claims/my")]
        public async Task<IActionResult> GetMyClaims()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var claims = await _claimsRepository.GetByCustomerIdAsync(userId);

            var result = claims.Select(c => new
            {
                c.ClaimId,
                c.ClaimNumber,
                c.PolicyId,
                c.CustomerId,
                ClaimType = c.claimType.ToString(),
                Status = c.Status.ToString(),
                ApprovedAmount = c.ApprovedAmount,
                c.RejectionReason,
                c.SettlementBreakdownJson,
                c.CreatedAt,
                Documents = c.Documents == null ? null : c.Documents.Select(d => new { d.Document1, d.Document2 }),
                Policy = c.Policy == null ? null : new
                {
                    c.Policy.PolicyId,
                    c.Policy.PolicyNumber,
                    InvoiceAmount = c.Policy.InvoiceAmount,
                    StartDate = c.Policy.StartDate,
                    EndDate = c.Policy.EndDate,
                    Plan = c.Policy.Plan == null ? null : new { c.Policy.Plan.PlanId, c.Policy.Plan.PlanName }
                },
                Vehicle = c.Policy?.Vehicle == null ? null : new
                {
                    c.Policy.Vehicle.VehicleId,
                    c.Policy.Vehicle.RegistrationNumber,
                    c.Policy.Vehicle.Make,
                    c.Policy.Vehicle.Model,
                    c.Policy.Vehicle.Year,
                    Application = c.Policy.Vehicle.VehicleApplication == null ? null : new { c.Policy.Vehicle.VehicleApplication.Make, c.Policy.Vehicle.VehicleApplication.Model, c.Policy.Vehicle.VehicleApplication.Year, Documents = c.Policy.Vehicle.VehicleApplication.Documents == null ? null : c.Policy.Vehicle.VehicleApplication.Documents.Select(d => d.FilePath) }
                }
            });

            return Ok(result);
        }

        [HttpPost("policy/cancel/{policyId}")]
        public async Task<IActionResult> CancelPolicy(int policyId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var policy = await _policyRepository.GetByIdAsync(policyId);
            if (policy == null || policy.CustomerId != userId)
                return NotFound(new { message = "Policy not found" });

            policy.Status = VIMS.Domain.Enums.PolicyStatus.Cancelled;
            await _policyRepository.UpdateAsync(policy);

            return Ok(new { message = "Policy cancelled" });
        }

        [HttpGet("payments/my")]
        public async Task<IActionResult> GetMyPayments()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var payments = await _paymentRepository.GetAllAsync();
            var myPayments = payments.Where(p => p.Policy != null && p.Policy.CustomerId == userId).Select(p => new
            {
                paymentId = p.PaymentId,
                policyId = p.PolicyId,
                amount = p.Amount,
                paymentDate = p.PaymentDate,
                status = p.Status.ToString(),
                transactionReference = p.TransactionReference,
                policyNumber = p.Policy?.PolicyNumber
            }).ToList();

            return Ok(myPayments);
        }

        // =====================================================
        // POLICY TRANSFER ENDPOINTS
        // =====================================================

        [HttpPost("transfer/initiate")]
        public async Task<IActionResult> InitiateTransfer([FromBody] InitiateTransferDTO dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _customerService.InitiateTransferAsync(dto, userId);

            if (result == "RECIPIENT_NOT_FOUND")
                return NotFound(new { message = "No customer account found with that email address." });

            return Ok(new { message = "Transfer request sent successfully." });
        }

        [HttpGet("transfer/incoming")]
        public async Task<IActionResult> GetIncomingTransfers()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _customerService.GetMyIncomingTransfersAsync(userId);
            return Ok(result);
        }

        [HttpGet("transfer/outgoing")]
        public async Task<IActionResult> GetOutgoingTransfers()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _customerService.GetMyOutgoingTransfersAsync(userId);
            return Ok(result);
        }

        [HttpPost("transfer/{transferId}/accept")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AcceptTransfer(int transferId, [FromForm] VIMS.Application.DTOs.AcceptTransferDTO dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var rcDocument = dto?.RcDocument;
            if (rcDocument == null || rcDocument.Length == 0)
                return BadRequest(new { message = "RC document is required." });

            await _customerService.AcceptTransferAsync(transferId, rcDocument, userId);
            return Ok(new { message = "Transfer accepted. A new application has been sent to the agent for approval." });
        }

        [HttpPost("transfer/{transferId}/reject")]
        public async Task<IActionResult> RejectTransfer(int transferId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _customerService.RejectTransferAsync(transferId, userId);
            return Ok(new { message = "Transfer request rejected." });
        }

        [HttpPost("roadside-assistance")]
        public async Task<IActionResult> RoadsideAssistance([FromBody] RoadsideAssistanceDTO dto)
        {
            var garages = await _garageRepository.GetAllAsync();
            if (garages == null || !garages.Any())
                return BadRequest("No active service centers available in the network.");

            // Find closest garage based on coordinates
            Garage? closestGarage = null;
            double minDistance = double.MaxValue;

            foreach (var g in garages)
            {
                var d = CalculateDistance(dto.Latitude, dto.Longitude, g.Latitude, g.Longitude);
                if (d < minDistance)
                {
                    minDistance = d;
                    closestGarage = g;
                }
            }

            if (closestGarage != null)
            {
                dto.GarageName = closestGarage.GarageName;
                
                // Keep only numeric digits from the stored phone number
                string digitsOnly = new string(closestGarage.PhoneNumber.Where(char.IsDigit).ToArray());
                
                // If it already starts with 91 and has 12 digits, just append '+'
                if (digitsOnly.Length == 12 && digitsOnly.StartsWith("91"))
                {
                    dto.GaragePhone = "+" + digitsOnly;
                }
                else
                {
                    // For standard 10-digit numbers or other lengths, ensure +91 prefix
                    // If it's 10 digits, we just prepend +91. 
                    // If it was e.g. 090144... we should probably trim the leading 0 if we are adding +91
                    string baseNumber = digitsOnly.Length > 10 && digitsOnly.StartsWith("0") ? digitsOnly.Substring(1) : digitsOnly;
                    dto.GaragePhone = "+91" + (baseNumber.Length > 10 && baseNumber.StartsWith("91") ? baseNumber.Substring(2) : baseNumber);
                }
                
                dto.Distance = Math.Round(minDistance, 2);
            }

            var client = _httpClientFactory.CreateClient();
            var n8nUrl = "http://localhost:5678/webhook/39b2cd79-6ddc-4b5c-b32d-3566e2a4becc";

            // Log the enriched request payload being sent to n8n
            Console.WriteLine("Sending Enriched Request to n8n Engine:");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            var response = await client.PostAsJsonAsync(n8nUrl, dto);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                
                // Log the response received from n8n
                Console.WriteLine("Received Response from n8n Engine:");
                Console.WriteLine(content);

                return Ok(content);
            }

            Console.WriteLine($"n8n Engine Error: {response.StatusCode}");
            return StatusCode((int)response.StatusCode, "Failed to connect to roadside assistance engine");
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Earth radius in km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double deg) => deg * (Math.PI / 180);
    }
}
