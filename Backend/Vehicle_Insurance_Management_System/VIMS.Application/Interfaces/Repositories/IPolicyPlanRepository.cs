using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IPolicyPlanRepository
    {
        public Task<PolicyPlan> GetPolicyPlanAsync(int id);
        Task UpdateAsync(PolicyPlan plan);
        Task<bool> ExistsAsync(int id);
    }
}
