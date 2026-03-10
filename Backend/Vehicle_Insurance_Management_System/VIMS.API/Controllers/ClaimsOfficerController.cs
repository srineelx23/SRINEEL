using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq;
using VIMS.Application.Interfaces.Services;
using VIMS.Application.DTOs;

namespace VIMS.API.Controllers
{
    [Authorize(Roles = "ClaimsOfficer")]
    [Route("api/[controller]")]
    [ApiController]
    public class ClaimsOfficerController : ControllerBase
    {
        private readonly IClaimsService _claimsService;

        public ClaimsOfficerController(IClaimsService claimsService)
        {
            _claimsService = claimsService;
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
                documents = claim.Documents?.Select(d => new { d.Document1, d.Document2 }),
                decisionType = claim.DecisionType,
                Customer = claim.Customer == null ? null : new { claim.Customer.UserId, claim.Customer.FullName, claim.Customer.Email },
                Policy = claim.Policy == null ? null : new
                {
                    claim.Policy.PolicyId,
                    claim.Policy.PolicyNumber,
                    claim.Policy.InvoiceAmount,
                    claim.Policy.StartDate,
                    claim.Policy.EndDate,
                    Plan = claim.Policy.Plan == null ? null : new { claim.Policy.Plan.PlanId, claim.Policy.Plan.PlanName }
                },
                Vehicle = claim.Policy?.Vehicle == null ? null : new
                {
                    claim.Policy.Vehicle.VehicleId,
                    claim.Policy.Vehicle.RegistrationNumber,
                    claim.Policy.Vehicle.Make,
                    claim.Policy.Vehicle.Model,
                    claim.Policy.Vehicle.Year,
                    Application = claim.Policy.Vehicle.VehicleApplication == null ? null : new
                    {
                        claim.Policy.Vehicle.VehicleApplication.Make,
                        claim.Policy.Vehicle.VehicleApplication.Model,
                        claim.Policy.Vehicle.VehicleApplication.Year,
                        Documents = claim.Policy.Vehicle.VehicleApplication.Documents == null
                            ? null
                            : claim.Policy.Vehicle.VehicleApplication.Documents.Select(d => d.FilePath)
                    }
                }
            };

            return Ok(result);
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
                    plan = c.Policy.Plan == null ? null : new { c.Policy.Plan.PlanId, c.Policy.Plan.PlanName }
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
