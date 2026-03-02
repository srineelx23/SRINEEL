using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Entities;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Repositories
{
     public class PolicyRepository : IPolicyRepository
    {
        private readonly VehicleInsuranceContext _vehicleInsuranceContext;
        public PolicyRepository(VehicleInsuranceContext vehicleInsuranceContext) {
            _vehicleInsuranceContext = vehicleInsuranceContext;
        }
        public async Task<Policy> AddAsync(Policy policy)
        {
            await _vehicleInsuranceContext.Policies.AddAsync(policy);
            await _vehicleInsuranceContext.SaveChangesAsync();
            return policy;
        }
        public async Task AddAsync(Policy policy, bool saveChanges)
        {
            await _vehicleInsuranceContext.Policies.AddAsync(policy);
            if (saveChanges)
                await _vehicleInsuranceContext.SaveChangesAsync();
        }

        public async Task AddAndExpireAsync(Policy newPolicy, Policy oldPolicy)
        {
            // perform both operations transactionally on the same DbContext
            await _vehicleInsuranceContext.Policies.AddAsync(newPolicy);
            oldPolicy.Status = VIMS.Domain.Enums.PolicyStatus.Expired;
            oldPolicy.IsRenewed = true;
            _vehicleInsuranceContext.Policies.Update(oldPolicy);
            await _vehicleInsuranceContext.SaveChangesAsync();
        }
        public async Task<List<Policy>> GetPoliciesByCustomerIdAsync(int customerId)
        {
            return await _vehicleInsuranceContext.Policies
                .Where(p => p.CustomerId == customerId)
                .Include(p => p.Vehicle)
                .Include(p => p.Plan)
                .ToListAsync();
        }

        public async Task<List<Policy>> GetAllAsync()
        {
            return await _vehicleInsuranceContext.Policies
                .Include(p => p.Vehicle)
                    .ThenInclude(v => v.VehicleApplication)
                    .ThenInclude(va => va.Documents)
                .Include(p => p.Plan)
                .Include(p => p.Customer)
                .ToListAsync();
        }
        public async Task<Policy?> GetByIdAsync(int policyId)
        {
            return await _vehicleInsuranceContext.Policies
                .Include(p => p.Vehicle)
                    .ThenInclude(v => v!.VehicleApplication)
                    .ThenInclude(a => a!.Documents)
                .Include(p => p.Plan)
                .FirstOrDefaultAsync(p => p.PolicyId == policyId);
        }

        public async Task UpdateAsync(Policy policy)
        {
            _vehicleInsuranceContext.Policies.Update(policy);
            await _vehicleInsuranceContext.SaveChangesAsync();
        }
    }
}
