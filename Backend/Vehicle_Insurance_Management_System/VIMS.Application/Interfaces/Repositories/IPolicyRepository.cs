using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IPolicyRepository
    {
        public Task<Policy> AddAsync(Policy policy);
        Task AddAsync(Policy policy, bool saveChanges);
        Task AddAndExpireAsync(Policy newPolicy, Policy oldPolicy);
        Task<List<Policy>> GetPoliciesByCustomerIdAsync(int customerId);
        Task<Policy?> GetByIdAsync(int policyId);
        Task UpdateAsync(Policy policy);
        Task<List<Policy>> GetAllAsync();
    }
}
