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
        public Task<User> CreateAgentAsync(RegisterDTO registerDTO);
        public Task<User> CreateClaimsOfficerAsync(RegisterDTO registerDTO);
        public Task<PolicyPlan> CreatePolicyPlanAsync(PolicyPlan policyPlan);
        public Task<List<PolicyPlan>> GetAllPolicyPlansAsync();
        public Task<PolicyPlan?> GetPolicyPlanByIdAsync(int planId);
        public Task<List<User>> GetAllUsersAsync();
    }
}
