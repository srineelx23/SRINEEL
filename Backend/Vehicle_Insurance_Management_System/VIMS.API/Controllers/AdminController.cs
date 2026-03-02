using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Enums;

namespace VIMS.API.Controllers
{
    [Authorize(Roles ="Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IPolicyPlanService _service;
        private readonly IClaimsRepository _claimsRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPolicyRepository _policyRepository;

        public AdminController(IAdminService adminService, IPolicyPlanService service, IClaimsRepository claimsRepository, IPaymentRepository paymentRepository, IPolicyRepository policyRepository)
        {
            _adminService = adminService;
            _service = service;
            _claimsRepository = claimsRepository;
            _paymentRepository = paymentRepository;
            _policyRepository = policyRepository;
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
            return Ok(users);
        }
        [HttpGet("claims")]
        public async Task<IActionResult> GetAllClaims()
        {
            var claims = await _claimsRepository.GetAllAsync();
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
                Policy = c.Policy == null ? null : new { c.Policy.PolicyId, c.Policy.PolicyNumber, c.Policy.InvoiceAmount },
                Documents = c.Documents != null ? c.Documents.Select(d => new { d.Document1, d.Document2 }) : null
            });
            return Ok(result);
        }

        [HttpGet("payments")]
        public async Task<IActionResult> GetAllPayments()
        {
            var payments = await _paymentRepository.GetAllAsync();
            var result = payments.Select(p => new
            {
                p.PaymentId,
                p.PolicyId,
                p.Amount,
                p.PaymentDate,
                Status = p.Status.ToString()
            });
            return Ok(result);
        }

        [HttpGet("policies")]
        public async Task<IActionResult> GetAllPolicies()
        {
            var policies = await _policyRepository.GetAllAsync();
            var result = policies.Select(p => new
            {
                p.PolicyId,
                p.PolicyNumber,
                Status = p.Status.ToString(),
                p.PremiumAmount,
                p.InvoiceAmount,
                p.StartDate,
                p.EndDate,
                p.SelectedYears,
                Vehicle = p.Vehicle == null ? null : new
                {
                    p.Vehicle.VehicleId,
                    p.Vehicle.Make,
                    p.Vehicle.Model,
                    p.Vehicle.Year,
                    p.Vehicle.RegistrationNumber,
                    Documents = p.Vehicle.VehicleApplication != null ? p.Vehicle.VehicleApplication.Documents.Select(d => new { d.DocumentType, d.FilePath }) : null
                },
                Customer = p.Customer == null ? null : new
                {
                    p.Customer.UserId,
                    p.Customer.FullName
                }
            });
            return Ok(result);
        }
        [HttpPut("deactivate/{id}")]
        public async Task<IActionResult> DeactivatePlan(int id)
        {
            var result = await _service.DeactivatePlanAsync(id);
            return Ok(new { message = result });
        }

        [HttpPut("activate/{id}")]
        public async Task<IActionResult> ActivatePlan(int id)
        {
            var result=await _service.ActivatePlanAsync(id);
            return Ok(new { message = result });
        }
    }
}
