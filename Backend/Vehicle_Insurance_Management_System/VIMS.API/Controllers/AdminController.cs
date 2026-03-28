using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IPolicyPlanService _policyPlanService;
        private readonly IClaimsService _claimsService;
        private readonly IAuditService _auditService;
        private readonly IInvoiceService _invoiceService;
        private readonly IGarageService _garageService;

        public AdminController(
            IAdminService adminService,
            IPolicyPlanService policyPlanService,
            IClaimsService claimsService,
            IAuditService auditService,
            IInvoiceService invoiceService,
            IGarageService garageService)
        {
            _adminService = adminService;
            _policyPlanService = policyPlanService;
            _claimsService = claimsService;
            _auditService = auditService;
            _invoiceService = invoiceService;
            _garageService = garageService;
        }

        [HttpPost("createAgent")]
        public async Task<IActionResult> CreateAgentAsync(RegisterDTO registerDTO)
        {
            var res = await _adminService.CreateAgentAsync(registerDTO);
            return Ok(res);
        }

        [HttpPost("createClaimsOfficer")]
        public async Task<IActionResult> CreateClaimsOfficer(RegisterDTO registerDTO)
        {
            var res = await _adminService.CreateClaimsOfficerAsync(registerDTO);
            return Ok(res);
        }

        [HttpPost("createPolicyPlan")]
        public async Task<IActionResult> CreatePolicyPlanAsync(PolicyPlan policyPlan)
        {
            var res = await _adminService.CreatePolicyPlanAsync(policyPlan);
            return Ok(res);
        }

        [HttpGet("policy-plans")]
        public async Task<IActionResult> GetAllPolicyPlans()
        {
            var plans = await _adminService.GetAllPolicyPlansAsync();
            return Ok(plans);
        }

        [HttpGet("policy-plan/{id}")]
        public async Task<IActionResult> GetPolicyPlanById(int id)
        {
            var plan = await _adminService.GetPolicyPlanByIdAsync(id);
            return Ok(plan);
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _adminService.GetAllUsersAsync();
            var result = users.Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Role,
                ReferralCode = u.Role == UserRole.Customer ? u.ReferralCode : null
            });
            return Ok(result);
        }

        [HttpGet("claims")]
        public async Task<IActionResult> GetAllClaims()
        {
            var claims = await _adminService.GetAllClaimsAsync();
            var result = claims.Select(c => new
            {
                c.ClaimId,
                c.ClaimNumber,
                c.PolicyId,
                c.CustomerId,
                ClaimType = c.claimType.ToString(),
                Status = c.Status.ToString(),
                c.ApprovedAmount,
                c.DecisionType,
                c.CreatedAt,
                Policy = c.Policy == null ? null : new { c.Policy.PolicyId, c.Policy.PolicyNumber, c.Policy.InvoiceAmount },
                Documents = c.Documents != null ? c.Documents.Select(d => new { d.Document1, d.Document2 }) : null
            });
            return Ok(result);
        }

        [HttpGet("payments")]
        public async Task<IActionResult> GetAllPayments()
        {
            var payments = await _adminService.GetAllPaymentsAsync();
            var result = payments.Select(p => new
            {
                p.PaymentId,
                p.PolicyId,
                p.Amount,
                p.PaymentDate,
                p.TransactionReference,
                Status = p.Status.ToString()
            });
            return Ok(result);
        }

        [HttpGet("policies")]
        public async Task<IActionResult> GetAllPolicies()
        {
            var policies = await _adminService.GetAllPoliciesAsync();
            var result = policies.Select(p => new
            {
                p.PolicyId,
                p.PolicyNumber,
                p.PlanId,
                Status = p.Status.ToString(),
                p.PremiumAmount,
                p.InvoiceAmount,
                p.IDV,
                p.StartDate,
                p.EndDate,
                p.SelectedYears,
                PolicyPlan = p.Plan == null ? null : new { p.Plan.PlanId, p.Plan.PlanName },
                Vehicle = p.Vehicle == null ? null : new
                {
                    p.Vehicle.VehicleId,
                    p.Vehicle.Make,
                    p.Vehicle.Model,
                    p.Vehicle.Year,
                    p.Vehicle.RegistrationNumber,
                    Documents = p.Vehicle.VehicleApplication != null
                        ? p.Vehicle.VehicleApplication.Documents.Select(d => new { d.DocumentType, d.FilePath })
                        : null
                },
                Customer = p.Customer == null ? null : new
                {
                    p.Customer.UserId,
                    p.Customer.FullName
                },
                Agent = p.Agent == null ? null : new
                {
                    p.Agent.UserId,
                    p.Agent.FullName
                }
            });
            return Ok(result);
        }

        [HttpPut("deactivate/{id}")]
        public async Task<IActionResult> DeactivatePlan(int id)
        {
            var result = await _policyPlanService.DeactivatePlanAsync(id);
            return Ok(new { message = result });
        }

        [HttpPut("activate/{id}")]
        public async Task<IActionResult> ActivatePlan(int id)
        {
            var result = await _policyPlanService.ActivatePlanAsync(id);
            return Ok(new { message = result });
        }

        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs()
        {
            var logs = await _auditService.GetAuditLogsAsync();
            return Ok(logs);
        }

        [HttpGet("transfers")]
        public async Task<IActionResult> GetAllTransfers()
        {
            var transfers = await _adminService.GetAllTransfersAsync();
            var result = transfers.Select(t => new
            {
                t.PolicyTransferId,
                t.PolicyId,
                t.SenderCustomerId,
                t.RecipientCustomerId,
                Status = t.Status.ToString(),
                t.CreatedAt,
                PolicyNumber = t.Policy?.PolicyNumber,
                SenderName = t.SenderCustomer?.FullName,
                RecipientName = t.RecipientCustomer?.FullName,
                Vehicle = t.Policy?.Vehicle == null ? null : new { t.Policy.Vehicle.Make, t.Policy.Vehicle.Model, t.Policy.Vehicle.RegistrationNumber }
            });
            return Ok(result);
        }

        [HttpGet("claim/download/{claimId}")]
        public async Task<IActionResult> DownloadClaimReport(int claimId)
        {
            // First check if claim exists to avoid generic 404
            var claim = await _claimsService.GetClaimByIdAsync(claimId);
            if (claim == null)
                return NotFound(new { message = $"Claim #{claimId} not found." });

            if (claim.Status != VIMS.Domain.Enums.ClaimStatus.Approved)
                return BadRequest(new { message = "Settlement reports are only available for approved claims." });

            var pdfBytes = _invoiceService.GenerateClaimSettlementPdf(claimId);
            if (pdfBytes == null || pdfBytes.Length == 0)
                return NotFound(new { message = "Settlement breakdown is missing for this claim." });

            return File(pdfBytes, "application/pdf", $"Claim_Settlement_{claimId}.pdf");
        }

        [HttpGet("invoice/download/{paymentId}")]
        public IActionResult DownloadInvoice(int paymentId)
        {
            var pdfBytes = _invoiceService.GenerateInvoicePdf(paymentId);
            if (pdfBytes == null || pdfBytes.Length == 0)
                return NotFound(new { message = "Invoice not found or could not be generated" });

            return File(pdfBytes, "application/pdf", $"Invoice_{paymentId}.pdf");
        }

        [HttpGet("transfer/download/{transferId}")]
        public IActionResult DownloadTransferReport(int transferId)
        {
            var pdfBytes = _invoiceService.GenerateTransferReportPdf(transferId);
            if (pdfBytes == null || pdfBytes.Length == 0)
                return NotFound(new { message = "Transfer certificate not found or could not be generated" });

            return File(pdfBytes, "application/pdf", $"Transfer_Certificate_{transferId}.pdf");
        }

        [HttpGet("policy/download/{policyId}")]
        public IActionResult DownloadPolicyContract(int policyId)
        {
            var pdfBytes = _invoiceService.GeneratePolicyContractPdf(policyId);
            if (pdfBytes == null || pdfBytes.Length == 0)
                return NotFound(new { message = "Policy contract not available" });

            return File(pdfBytes, "application/pdf", $"Policy_Contract_{policyId}.pdf");
        }

        // ================= GARAGE CRUD =================

        [HttpGet("garages")]
        public async Task<IActionResult> GetGarages()
        {
            var garages = await _garageService.GetAllGaragesAsync();
            return Ok(garages);
        }

        [HttpGet("garage/{id}")]
        public async Task<IActionResult> GetGarage(int id)
        {
            var garage = await _garageService.GetGarageByIdAsync(id);
            if (garage == null) return NotFound();
            return Ok(garage);
        }

        [HttpPost("garage")]
        public async Task<IActionResult> AddGarage(Garage garage)
        {
            var created = await _garageService.CreateGarageAsync(garage);
            return CreatedAtAction(nameof(GetGarage), new { id = created.GarageId }, created);
        }

        [HttpPut("garage/{id}")]
        public async Task<IActionResult> UpdateGarage(int id, Garage garage)
        {
            if (id != garage.GarageId) return BadRequest();
            await _garageService.UpdateGarageAsync(garage);
            return NoContent();
        }

        [HttpDelete("garage/{id}")]
        public async Task<IActionResult> DeleteGarage(int id)
        {
            await _garageService.DeleteGarageAsync(id);
            return NoContent();
        }
    }
}
