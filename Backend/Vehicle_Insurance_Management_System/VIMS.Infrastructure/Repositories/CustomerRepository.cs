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
    public class CustomerRepository:ICustomerRepository
    {
        private readonly VehicleInsuranceContext _context;
        public CustomerRepository(VehicleInsuranceContext context)
        {
            _context = context;
        }
        public async Task<List<PolicyPlan>> ViewAllPolicyPlansAsync()
        {
            return await _context.PolicyPlans.ToListAsync();
        }
    }
}
