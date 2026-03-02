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
    public class AdminRepository:IAdminRepository
    {
        private readonly VehicleInsuranceContext _context;
        public AdminRepository(VehicleInsuranceContext context)
        {
            _context = context;
        }
        public async Task<User> CreateAgentAsync(User agent)
        {
            _context.Users.Add(agent);
            _context.SaveChanges();
            return agent;
        }

        public async Task<User> CreateClaimsOfficerAsync(User claimsOfficer)
        {
            _context.Users.Add(claimsOfficer);
            _context.SaveChanges();
            return claimsOfficer;
        }

        public async Task<PolicyPlan> CreatePolicyPlanAsync(PolicyPlan policyPlan)
        {
            _context.PolicyPlans.Add(policyPlan);
            _context.SaveChanges();
            return policyPlan;
        }
        public async Task<List<PolicyPlan>> GetAllPolicyPlansAsync()
        {
            return await _context.PolicyPlans.ToListAsync();
        }
        public async Task<PolicyPlan?> GetPolicyPlanByIdAsync(int planId)
        {
            return await _context.PolicyPlans
                .Include(p => p.Policies)
                .FirstOrDefaultAsync(p => p.PlanId == planId);
        }
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }
    }
}
