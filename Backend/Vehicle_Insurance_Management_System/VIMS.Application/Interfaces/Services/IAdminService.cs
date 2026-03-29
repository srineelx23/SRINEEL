using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Services
{
    public interface IAdminService
    {
        public Task<ProvisioningResultDTO> CreateAgentAsync(RegisterDTO registerDTO);
        public Task<ProvisioningResultDTO> CreateClaimsOfficerAsync(RegisterDTO registerDTO);

        public Task<PolicyPlan> CreatePolicyPlanAsync(PolicyPlan policyPlan);
        public Task<List<PolicyPlan>> GetAllPolicyPlansAsync();
        public Task<PolicyPlan?> GetPolicyPlanByIdAsync(int planId);
        public Task<List<User>> GetAllUsersAsync();

        // Admin-level read operations (previously pulled from repos directly in controller)
        Task<List<Claims>> GetAllClaimsAsync();
        Task<List<Payment>> GetAllPaymentsAsync();
        Task<List<Policy>> GetAllPoliciesAsync();
        Task<List<VehicleApplication>> GetAllVehicleApplicationsAsync();
        Task<List<PolicyTransfer>> GetAllTransfersAsync();
    }
}
