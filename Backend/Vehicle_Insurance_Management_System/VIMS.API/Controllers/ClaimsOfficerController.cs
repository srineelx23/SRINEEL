using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq;
using VIMS.Application.Interfaces.Services;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Entities;

namespace VIMS.API.Controllers
{
    [Authorize(Roles = "ClaimsOfficer")]
    [Route("api/[controller]")]
    [ApiController]
    public class ClaimsOfficerController : ControllerBase
    {
        private readonly IClaimsService _claimsService;
        private readonly IPolicyTransferRepository _transferRepository;
        private readonly IInvoiceService _invoiceService;

        public ClaimsOfficerController(IClaimsService claimsService, IPolicyTransferRepository transferRepository, IInvoiceService invoiceService)
        {
            _claimsService = claimsService;
            _transferRepository = transferRepository;
            _invoiceService = invoiceService;
        }

        [HttpGet("policy/download/{policyId}")]
        public IActionResult DownloadPolicyContract(int policyId)
        {
            var pdfBytes = _invoiceService.GeneratePolicyContractPdf(policyId);
            if (pdfBytes == null || pdfBytes.Length == 0)
                return NotFound(new { message = "Policy contract not available" });

            return File(pdfBytes, "application/pdf", $"Policy_Contract_{policyId}.pdf");
        }

        [HttpGet("claim/download-settlement/{claimId}")]
        public IActionResult DownloadSettlementReport(int claimId)
        {
            var pdfBytes = _invoiceService.GenerateClaimSettlementPdf(claimId);
            if (pdfBytes == null || pdfBytes.Length == 0)
                return NotFound(new { message = "Settlement report not available for this claim" });

            return File(pdfBytes, "application/pdf", $"Claim_Settlement_{claimId}.pdf");
        }

        [HttpGet("claim/{id}")]
        public async Task<IActionResult> GetClaimDetails(int id)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue))
                return Unauthorized(new { message = "UserId claim missing in token" });

            int officerId = int.Parse(userIdValue);

            var claim = await _claimsService.GetClaimByIdAsync(id);
            if (claim == null)
                return NotFound(new { message = "Claim not found" });

            if (claim.ClaimsOfficerId != officerId && !User.IsInRole("Admin"))
                return Forbid();

            var result = new
            {
                claimId = claim.ClaimId,
                claimNumber = claim.ClaimNumber,
                policyId = claim.PolicyId,
                customerId = claim.CustomerId,
                claimType = claim.claimType.ToString(),
                status = claim.Status.ToString(),
                createdAt = claim.CreatedAt,
                approvedAmount = claim.ApprovedAmount,
                rejectionReason = claim.RejectionReason,
                documents = claim.Documents?.Select(d => new
                {
                    document1 = d.Document1,
                    document2 = d.Document2
                }),
                decisionType = claim.DecisionType,
                Customer = new
                {
                    fullName = claim.Customer?.FullName,
                    email = claim.Customer?.Email
                },
                Policy = new
                {
                    policyNumber = claim.Policy?.PolicyNumber,
                    startDate = claim.Policy?.StartDate,
                    endDate = claim.Policy?.EndDate,
                    premiumAmount = claim.Policy?.PremiumAmount,
                    status = claim.Policy?.Status.ToString(),
                    isPaid = claim.Policy?.IsCurrentYearPaid
                },
                Vehicle = new
                {
                    make = claim.Policy?.Vehicle?.Make,
                    model = claim.Policy?.Vehicle?.Model,
                    year = claim.Policy?.Vehicle?.Year,
                    registrationNumber = claim.Policy?.Vehicle?.RegistrationNumber,
                        vehicleApplication = new
                        {
                            documents = claim.Policy?.Vehicle?.VehicleApplication == null
                                ? null
                                : claim.Policy.Vehicle.VehicleApplication.Documents.Select(d => new { d.DocumentType, d.FilePath })
                        }
                },
                fraudRiskScore = 0,
                riskReasons = new List<string>()
            };

            // 4. NEW: Integrated AI-Analysis (Groq + OCR + History)
            var analysis = await _claimsService.AnalyzeClaimAsync(id);

            // Final Assignment
            var finalResult = new
            {
                claimId = result.claimId,
                claimNumber = result.claimNumber,
                policyId = result.policyId,
                customerId = result.customerId,
                claimType = result.claimType,
                status = result.status,
                createdAt = result.createdAt,
                approvedAmount = result.approvedAmount,
                rejectionReason = result.rejectionReason,
                documents = result.documents,
                decisionType = result.decisionType,
                customer = result.Customer,
                policy = result.Policy,
                vehicle = result.Vehicle,
                fraudRiskScore = analysis.FraudRiskScore,
                riskReasons = analysis.RiskReasons,
                summary = analysis.Summary,
                suggestedRepairCost = analysis.SuggestedRepairCost,
                suggestedManufactureYear = analysis.SuggestedManufactureYear
            };

            return Ok(finalResult);
        }

        [HttpPost("payout-breakdown/{claimId}")]
        public async Task<IActionResult> GetPayoutBreakdown(int claimId, [FromBody] ApproveClaimDTO dto)
        {
            try
            {
                var breakdown = await _claimsService.CalculateClaimBreakdownAsync(claimId, dto);
                return Ok(breakdown);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("all-claims")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllClaims()
        {
            var claims = await _claimsService.GetAllClaimsAsync();
            return Ok(claims);
        }

        [HttpGet("my-claims")]
        public async Task<IActionResult> GetMyAssignedClaims()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue))
                return Unauthorized(new { message = "UserId claim missing in token" });

            int officerId = int.Parse(userIdValue);

            var claims = await _claimsService.GetClaimsByOfficerIdAsync(officerId);

            var result = claims.Select(c => new
            {
                claimId = c.ClaimId,
                claimNumber = c.ClaimNumber,
                policyId = c.PolicyId,
                customerId = c.CustomerId,
                claimType = c.claimType.ToString(),
                status = c.Status.ToString(),
                createdAt = c.CreatedAt,
                approvedAmount = c.ApprovedAmount,
                rejectionReason = c.RejectionReason,
                documents = c.Documents == null ? null : c.Documents.Select(d => new { d.Document1, d.Document2 }),
                customer = c.Customer == null ? null : new { c.Customer.UserId, c.Customer.FullName, c.Customer.Email },
                policy = c.Policy == null ? null : new
                {
                    c.Policy.PolicyId,
                    c.Policy.PolicyNumber,
                    c.Policy.PremiumAmount,
                    InvoiceAmount = c.Policy.InvoiceAmount,
                    c.Policy.StartDate,
                    c.Policy.EndDate,
                    plan = c.Policy.Plan == null ? null : new 
                    { 
                        c.Policy.Plan.PlanId, 
                        c.Policy.Plan.PlanName,
                        c.Policy.Plan.DeductibleAmount,
                        c.Policy.Plan.MaxCoverageAmount,
                        c.Policy.Plan.ZeroDepreciationAvailable,
                        c.Policy.Plan.EngineProtectionAvailable
                    }
                },
                vehicle = c.Policy?.Vehicle == null ? null : new
                {
                    c.Policy.Vehicle.VehicleId,
                    c.Policy.Vehicle.RegistrationNumber,
                    c.Policy.Vehicle.Make,
                    c.Policy.Vehicle.Model,
                    c.Policy.Vehicle.Year,
                    invoiceAmount = c.Policy.InvoiceAmount,
                    application = c.Policy.Vehicle.VehicleApplication == null ? null : new
                    {
                        c.Policy.Vehicle.VehicleApplication.Make,
                        c.Policy.Vehicle.VehicleApplication.Model,
                        c.Policy.Vehicle.VehicleApplication.Year,
                        documents = c.Policy.Vehicle.VehicleApplication.Documents == null
                            ? null
                            : c.Policy.Vehicle.VehicleApplication.Documents.Select(d => d.FilePath)
                    }
                }
            });

            return Ok(result);
        }

        [HttpPost("decide/{claimId}")]
        public async Task<IActionResult> DecideClaim(int claimId, [FromBody] ApproveClaimDTO dto, [FromQuery] bool approve = true)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdValue))
                return Unauthorized(new { message = "UserId claim missing in token" });

            int officerId = int.Parse(userIdValue);

            var res = await _claimsService.DecideClaimAsync(claimId, dto, officerId, approve);

            return Ok(new { message = res });
        }
    }
}
