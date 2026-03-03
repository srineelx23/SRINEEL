using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.Exceptions;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.Application.Services
{
    public class PolicyPlanService:IPolicyPlanService
    {
        private readonly IPolicyPlanRepository _repository;
        private readonly IAuditService _auditService;

        public PolicyPlanService(IPolicyPlanRepository repository, IAuditService auditService)
        {
            _repository = repository;
            _auditService = auditService;
        }
        public async Task<string> DeactivatePlanAsync(int planId)
        {
            var plan = await _repository.GetPolicyPlanAsync(planId);

            if (plan == null)
                throw new NotFoundException("Policy plan not found.");

            if (plan.Status == PlanStatus.Inactive)
                throw new BadRequestException("Policy plan is already inactive.");

            plan.Status = PlanStatus.Inactive;

            await _repository.UpdateAsync(plan);
            await _auditService.LogActionAsync("PolicyPlanDeactivated", "Admin", $"Deactivated policy plan: {plan.PlanName}", "PolicyPlan", plan.PlanId.ToString());

            return "Policy plan deactivated successfully.";
        }

        public async Task<string> ActivatePlanAsync(int planId)
        {
            var plan = await _repository.GetPolicyPlanAsync(planId);

            if (plan == null)
                throw new NotFoundException("Policy plan not found.");

            if (plan.Status == PlanStatus.Active)
                throw new BadRequestException("Policy plan is already active.");

            plan.Status = PlanStatus.Active;

            await _repository.UpdateAsync(plan);
            await _auditService.LogActionAsync("PolicyPlanActivated", "Admin", $"Activated policy plan: {plan.PlanName}", "PolicyPlan", plan.PlanId.ToString());

            return "Policy plan Activated successfully.";
        }

        public async Task<PolicyPlan> GetPolicyPlanAsync(int planId)
        {
            var plan = await _repository.GetPolicyPlanAsync(planId);
            return plan;
        }
    }
}
