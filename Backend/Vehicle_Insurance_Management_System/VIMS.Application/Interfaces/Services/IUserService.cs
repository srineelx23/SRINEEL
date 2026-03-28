using VIMS.Domain.DTOs;
using VIMS.Domain.Enums;

namespace VIMS.Application.Interfaces.Services
{
    public interface IUserService
    {
        Task<UserContextDto?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserContextDto>> GetRecentUsersAsync(int take = 20, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserContextDto>> GetUsersByRoleAsync(UserRole role, int take = 200, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ReferralAbuseSignalDto>> GetPotentialReferralAbuseUsersAsync(CancellationToken cancellationToken = default);
        Task<int> GetTotalUsersCountAsync(CancellationToken cancellationToken = default);
    }
}
