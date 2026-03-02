using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Services
{
    public interface ICustomerService
    {
        Task CreateApplicationAsync(CreateVehicleApplicationDTO dto,int userId);
        public Task<List<PolicyPlan>> ViewAllPoliciesAsync();
        public Task<List<CustomerApplicationDTO>> GetMyApplicationsAsync(int customerId);
        Task<List<CustomerPolicyDTO>> GetMyPoliciesAsync(int customerId);
        public Task<string> RenewPolicyAsync(int policyId,RenewPolicyDTO dto,int customerId);
        public Task<string> PayAnnualPremiumAsync(int policyId, int customerId);
    }
}
