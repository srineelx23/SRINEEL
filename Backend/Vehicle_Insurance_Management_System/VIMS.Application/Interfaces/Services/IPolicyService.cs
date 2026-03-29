using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IPolicyService
    {
        Task<PolicyContextDto?> GetPolicyByIdAsync(int policyId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PolicyContextDto>> GetPoliciesByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PolicyContextDto>> GetPoliciesExpiringInRangeAsync(DateTime fromInclusiveUtc, DateTime toInclusiveUtc, int take = 10, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PolicyContextDto>> GetZeroDepreciationPoliciesWithVehiclesAsync(int take = 10, CancellationToken cancellationToken = default);
        Task<decimal> GetTotalPremiumAmountAsync(int? userId = null, bool pendingPaymentOnly = false, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PolicyContextDto>> GetRelevantPoliciesAsync(bool pendingPaymentOnly = false, bool highestIdvFirst = false, int take = 10, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PolicyContextDto>> GetPendingPaymentPoliciesAsync(int take = 200, CancellationToken cancellationToken = default);
        Task<PolicyContextDto?> GetPolicyWithHighestIdvAsync(CancellationToken cancellationToken = default);
        Task<PolicyContextDto?> GetPolicyWithHighestIdvByFiltersAsync(string? vehicleType, string? fuelType, string? planType, CancellationToken cancellationToken = default);
        Task<PolicyContextDto?> GetPolicyWithHighestPremiumAsync(CancellationToken cancellationToken = default);
        Task<int> GetSoldPoliciesCountByPolicyTypeAsync(string policyType, bool includePendingPayment = false, CancellationToken cancellationToken = default);
        Task<int> GetTotalPoliciesCountAsync(string? planNameContains = null, CancellationToken cancellationToken = default);
        Task<int> GetRegisteredVehiclesCountAsync(bool activePoliciesOnly = false, CancellationToken cancellationToken = default);
        Task<decimal> GetTotalCollectedPremiumsAsync(bool activePoliciesOnly = false, CancellationToken cancellationToken = default);
    }
}
