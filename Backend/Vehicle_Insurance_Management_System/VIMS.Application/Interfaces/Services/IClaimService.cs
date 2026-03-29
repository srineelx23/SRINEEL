using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IClaimService
    {
        Task<ClaimContextDto?> GetClaimByIdAsync(int claimId, CancellationToken cancellationToken = default);
        Task<ClaimContextDto?> GetClaimByNumberAsync(string claimNumber, CancellationToken cancellationToken = default);
        Task<(int UserId, int ClaimCount)?> GetUserWithMostClaimsAsync(CancellationToken cancellationToken = default);
        Task<(int PolicyId, int ClaimCount)?> GetPolicyWithMostClaimsAsync(CancellationToken cancellationToken = default);
        Task<ClaimContextDto?> GetHighestPayoutClaimAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ClaimContextDto>> GetClaimsByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ClaimContextDto>> GetRejectedClaimsForDateAsync(DateTime utcDate, int take = 10, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ClaimContextDto>> GetRelevantClaimsAsync(bool rejectedOnly = false, int take = 10, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ClaimContextDto>> GetClaimsForAnalyticsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ClaimContextDto>> GetRecentRejectedClaimsAsync(int take = 10, CancellationToken cancellationToken = default);
        Task<int> GetTotalClaimsCountAsync(CancellationToken cancellationToken = default);
    }
}
