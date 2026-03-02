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
    public class PolicyPlanRepository:IPolicyPlanRepository
    {
        private readonly VehicleInsuranceContext _context;
        public PolicyPlanRepository(VehicleInsuranceContext context) {
            _context = context;
        }

        public async Task<PolicyPlan> GetPolicyPlanAsync(int id)
        {
            return await _context.PolicyPlans.FindAsync(id);
        }
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.PolicyPlans
                .AnyAsync(p => p.PlanId == id);
        }

        public async Task UpdateAsync(PolicyPlan plan)
        {
            _context.PolicyPlans.Update(plan);
            await _context.SaveChangesAsync();
        }
    }
}
