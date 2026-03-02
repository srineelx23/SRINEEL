using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface ICustomerRepository
    {
        public Task<List<PolicyPlan>> ViewAllPolicyPlansAsync();

        
    }
}
