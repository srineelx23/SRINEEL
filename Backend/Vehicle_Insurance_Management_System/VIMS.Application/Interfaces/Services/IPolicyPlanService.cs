using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Services
{
    public interface IPolicyPlanService
    {
        Task<string> DeactivatePlanAsync(int planId);
        Task<string> ActivatePlanAsync(int planId);
        public Task<PolicyPlan> GetPolicyPlanAsync(int planId);
    }
}
