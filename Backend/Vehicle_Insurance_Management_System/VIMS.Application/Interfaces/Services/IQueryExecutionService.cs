using VIMS.Application.DTOs;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Services
{
    public interface IQueryExecutionService
    {
        Task<string?> ExecuteAsync(
            string query,
            AgentDecision decision,
            int userId,
            string role,
            List<Policy> policies,
            List<Claims> claims,
            List<VehicleApplication> applications,
            List<Payment> payments,
            List<PolicyPlan> plans);
    }
}
