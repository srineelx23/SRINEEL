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

        public ClaimsOfficerController(IClaimsService claimsService, IPolicyTransferRepository transferRepository)
        {
            _claimsService = claimsService;
            _transferRepository = transferRepository;
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
                            : claim.Policy.Vehicle.VehicleApplication.Documents.Select(d => d.FilePath)
                    }
                },
                fraudRiskScore = 0,
                riskReasons = new List<string>()
            };

            // Fraud Score Calculation Logic
            int score = 0;
            var reasons = new List<string>();

            // 1. Instant Claim Pattern (+40) - Within 48 hours of any PAID policy payment
            var payments = claim.Policy?.Payments?.ToList() ?? new List<Payment>();
            var lastPayment = payments
                .Where(p => p.Status == VIMS.Domain.Enums.PaymentStatus.Paid)
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefault();

            if (lastPayment != null && Math.Abs((claim.CreatedAt - lastPayment.PaymentDate).TotalHours) < 48)
            {
                score += 40;
                reasons.Add("Claim filed within 48 hours of a premium payment.");
            }

            // 2. Post-Transfer Alert (+25) - Within 30 days of a completed transfer
            var transfers = await _transferRepository.GetTransfersByPolicyIdAsync(claim.PolicyId);
            var lastTransfer = transfers?
                .Where(t => t.Status == VIMS.Domain.Enums.PolicyTransferStatus.Completed)
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .FirstOrDefault();

            if (lastTransfer != null && Math.Abs((claim.CreatedAt - (lastTransfer.UpdatedAt ?? lastTransfer.CreatedAt)).TotalDays) < 30)
            {
                score += 25;
                reasons.Add("Claim filed within 30 days of a policy transfer completion.");
            }
            
            // 3. Closing Window Pattern (+20) - Within last 10 days of policy term
            if (claim.Policy != null && (claim.Policy.CurrentYearEndDate - claim.CreatedAt).TotalDays < 10 && (claim.Policy.CurrentYearEndDate - claim.CreatedAt).TotalDays >= 0)
            {
                score += 20;
                reasons.Add("Claim filed within the last 10 days of the policy term.");
            }

            // 4. Frequent Filer & Rejection History (Dynamic Scoring)
            var otherClaims = claim.Customer?.CustomerClaims?.ToList() ?? new List<Claims>();
            
            // Total claims in last 6 months (Frequency risk)
            var recentTotalCount = otherClaims
                .Count(c => c.ClaimId != claim.ClaimId && Math.Abs((claim.CreatedAt - c.CreatedAt).TotalDays) < 180);

            // Rejected claims in last 12 months (Suspicious pattern risk)
            var recentRejectedCount = otherClaims
                .Count(c => c.ClaimId != claim.ClaimId 
                            && c.Status == VIMS.Domain.Enums.ClaimStatus.Rejected 
                            && Math.Abs((claim.CreatedAt - c.CreatedAt).TotalDays) < 365);

            if (recentTotalCount > 0)
            {
                // Give 10 points per claim, max 30 for frequency
                int freqScore = Math.Min(recentTotalCount * 10, 30);
                score += freqScore;
                reasons.Add($"High Frequency: Customer has filed {recentTotalCount} other claim(s) in the last 6 months.");
            }

            if (recentRejectedCount >= 2)
            {
                // Extra 20 points if they have a history of failing to get claims approved
                score += 20;
                reasons.Add($"Suspicious History: {recentRejectedCount} previous claims from this customer were REJECTED in the last year.");
            }

            // Final Assignment
            var finalResult = new
            {
                result.claimId, result.claimNumber, result.policyId, result.customerId, result.claimType,
                result.status, result.createdAt, result.approvedAmount, result.rejectionReason,
                result.documents, result.decisionType, result.Customer, result.Policy, result.Vehicle,
                fraudRiskScore = Math.Min(score, 100),
                riskReasons = reasons
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
