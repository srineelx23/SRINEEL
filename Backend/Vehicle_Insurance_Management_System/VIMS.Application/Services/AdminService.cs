using AutoMapper;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Application.Exceptions;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using System.Net.Http;
using System.Text.Json;


namespace VIMS.Application.Services
{
    public class AdminService : IAdminService
    {
        private readonly IAdminRepository _adminRepository;
        private readonly IAuthRepository _authRepository;
        private readonly IMapper _mapper;
        private readonly IAuditService _auditService;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly IClaimsRepository _claimsRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPolicyRepository _policyRepository;
        private readonly IVehicleApplicationRepository _vehicleApplicationRepository;
        private readonly IPolicyTransferRepository _transferRepository;
        private readonly IHttpClientFactory _httpClientFactory;

        public AdminService(
            IAdminRepository repository,
            IAuthRepository authRepository,
            IMapper mapper,
            IAuditService auditService,
            IClaimsRepository claimsRepository,
            IPaymentRepository paymentRepository,
            IPolicyRepository policyRepository,
            IVehicleApplicationRepository vehicleApplicationRepository,
            IPolicyTransferRepository transferRepository,
            IHttpClientFactory httpClientFactory)
        {
            _authRepository = authRepository;
            _adminRepository = repository;
            _mapper = mapper;
            _auditService = auditService;
            _claimsRepository = claimsRepository;
            _paymentRepository = paymentRepository;
            _policyRepository = policyRepository;
            _vehicleApplicationRepository = vehicleApplicationRepository;
            _transferRepository = transferRepository;
            _httpClientFactory = httpClientFactory;
            _passwordHasher = new PasswordHasher<User>();
        }

        private const string DefaultPassword = "DefaultPassword@123";
        private const string WebhookUrl = "http://localhost:5678/webhook/send-account-email";


        public async Task<ProvisioningResultDTO> CreateAgentAsync(RegisterDTO registerDTO)
        {
            var res = await _authRepository.UserExistsAsync(registerDTO.Email);
            if (res != null)
            {
                throw new BadRequestException("Agent already Exists");
            }
            var createdAgent = _mapper.Map<User>(registerDTO);
            createdAgent.Role = UserRole.Agent;
            createdAgent.IsFirstLogin = true;
            createdAgent.PasswordHash = _passwordHasher.HashPassword(createdAgent, DefaultPassword);
            if (!string.IsNullOrEmpty(registerDTO.SecurityAnswer))
            {
                createdAgent.SecurityAnswerHash = _passwordHasher.HashPassword(createdAgent, registerDTO.SecurityAnswer.Trim().ToLower());
            }
            var result = await _adminRepository.CreateAgentAsync(createdAgent);
            var webhookMsg = await NotifyN8nAsync(result.FullName, result.Email, "Agent", DefaultPassword);
            await _auditService.LogActionAsync("AgentCreated", "Admin", $"Admin created agent: {result.Email}", "User", result.UserId.ToString());
            return new ProvisioningResultDTO { User = result, WebhookResponse = webhookMsg };
        }



        public async Task<ProvisioningResultDTO> CreateClaimsOfficerAsync(RegisterDTO registerDTO)
        {
            var res = await _authRepository.UserExistsAsync(registerDTO.Email);
            if (res != null)
            {
                throw new BadRequestException("Claims Officer Already Exists");
            }
            var createdClaimsOfficer = _mapper.Map<User>(registerDTO);
            createdClaimsOfficer.Role = UserRole.ClaimsOfficer;
            createdClaimsOfficer.IsFirstLogin = true;
            createdClaimsOfficer.PasswordHash = _passwordHasher.HashPassword(createdClaimsOfficer, DefaultPassword);
            if (!string.IsNullOrEmpty(registerDTO.SecurityAnswer))
            {
                createdClaimsOfficer.SecurityAnswerHash = _passwordHasher.HashPassword(createdClaimsOfficer, registerDTO.SecurityAnswer.Trim().ToLower());
            }
            var result = await _adminRepository.CreateClaimsOfficerAsync(createdClaimsOfficer);
            var webhookMsg = await NotifyN8nAsync(result.FullName, result.Email, "ClaimsOfficer", DefaultPassword);
            await _auditService.LogActionAsync("ClaimsOfficerCreated", "Admin", $"Admin created claims officer: {result.Email}", "User", result.UserId.ToString());
            return new ProvisioningResultDTO { User = result, WebhookResponse = webhookMsg };
        }


        private async Task<string> NotifyN8nAsync(string username, string email, string role, string password)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var payload = new
                {
                    username,
                    email,
                    role,
                    password
                };
                var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync(WebhookUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                return "Account provisioned but automated notification failed.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to notify n8n: {ex.Message}");
                return "Account provisioned. Webhook notification error.";
            }
        }



        public async Task<PolicyPlan> CreatePolicyPlanAsync(PolicyPlan policyPlan)
        {
            var result = await _adminRepository.CreatePolicyPlanAsync(policyPlan);
            await _auditService.LogActionAsync("PolicyPlanCreated", "Admin", $"Admin created policy plan: {result.PlanName}", "PolicyPlan", result.PlanId.ToString());
            return result;
        }

        public async Task<List<PolicyPlan>> GetAllPolicyPlansAsync()
        {
            return await _adminRepository.GetAllPolicyPlansAsync();
        }

        public async Task<PolicyPlan?> GetPolicyPlanByIdAsync(int planId)
        {
            var res = await _adminRepository.GetPolicyPlanByIdAsync(planId);
            if (res == null)
            {
                throw new NotFoundException("Plan does not exist");
            }
            return res;
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _adminRepository.GetAllUsersAsync();
        }

        public async Task<List<Claims>> GetAllClaimsAsync()
        {
            return await _claimsRepository.GetAllAsync();
        }

        public async Task<List<Payment>> GetAllPaymentsAsync()
        {
            return await _paymentRepository.GetAllAsync();
        }

        public async Task<List<Policy>> GetAllPoliciesAsync()
        {
            return await _policyRepository.GetAllAsync();
        }

        public async Task<List<VehicleApplication>> GetAllVehicleApplicationsAsync()
        {
            return await _vehicleApplicationRepository.GetAllAsync();
        }

        public async Task<List<PolicyTransfer>> GetAllTransfersAsync()
        {
            return await _transferRepository.GetAllAsync();
        }
    }
}
