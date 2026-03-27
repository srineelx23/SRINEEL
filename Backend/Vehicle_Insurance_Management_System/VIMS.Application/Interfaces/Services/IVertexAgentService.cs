using VIMS.Application.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IVertexAgentService
    {
        Task<AgentDecision?> GetDecisionAsync(string query, CancellationToken cancellationToken = default);
    }
}
