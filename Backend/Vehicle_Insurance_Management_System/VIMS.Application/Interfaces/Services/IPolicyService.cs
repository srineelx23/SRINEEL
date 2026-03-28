using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IPolicyService
    {
        Task<PolicyContextDto?> GetPolicyByIdAsync(int policyId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PolicyContextDto>> GetPoliciesByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PolicyContextDto>> GetRecentPoliciesAsync(int take = 20, CancellationToken cancellationToken = default);
        Task<int> GetTotalPoliciesCountAsync(string? planNameContains = null, CancellationToken cancellationToken = default);
        Task<int> GetRegisteredVehiclesCountAsync(bool activePoliciesOnly = false, CancellationToken cancellationToken = default);
        Task<decimal> GetTotalCollectedPremiumsAsync(bool activePoliciesOnly = false, CancellationToken cancellationToken = default);
    }
}
