using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IClaimService
    {
        Task<ClaimContextDto?> GetClaimByIdAsync(int claimId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ClaimContextDto>> GetClaimsByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ClaimContextDto>> GetRecentClaimsAsync(int take = 20, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ClaimContextDto>> GetRecentRejectedClaimsAsync(int take = 10, CancellationToken cancellationToken = default);
        Task<int> GetTotalClaimsCountAsync(CancellationToken cancellationToken = default);
    }
}
