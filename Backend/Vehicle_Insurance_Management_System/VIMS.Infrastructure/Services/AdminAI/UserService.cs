using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;
using VIMS.Domain.Enums;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Services.AdminAI
{
    public class UserService : IUserService
    {
        private readonly VehicleInsuranceContext _context;

        public UserService(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<UserContextDto?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new UserContextDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = u.Role.ToString(),
                    IsActive = u.IsActive,
                    ReferralCode = u.ReferralCode,
                    ReferredByUserId = u.ReferredByUserId,
                    HasUsedReferral = u.HasUsedReferral
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<UserContextDto>> GetRecentUsersAsync(int take = 20, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 20 : take;
            return await _context.Users
                .AsNoTracking()
                .OrderByDescending(u => u.UserId)
                .Take(safeTake)
                .Select(u => new UserContextDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = u.Role.ToString(),
                    IsActive = u.IsActive,
                    ReferralCode = u.ReferralCode,
                    ReferredByUserId = u.ReferredByUserId,
                    HasUsedReferral = u.HasUsedReferral
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<UserContextDto>> GetUsersByRoleAsync(UserRole role, int take = 200, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 200 : take;
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.Role == role)
                .OrderBy(u => u.FullName)
                .Take(safeTake)
                .Select(u => new UserContextDto
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = u.Role.ToString(),
                    IsActive = u.IsActive,
                    ReferralCode = u.ReferralCode,
                    ReferredByUserId = u.ReferredByUserId,
                    HasUsedReferral = u.HasUsedReferral
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ReferralAbuseSignalDto>> GetPotentialReferralAbuseUsersAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow.AddDays(-30);

            var grouped = await _context.Referrals
                .AsNoTracking()
                .Where(r => r.CreatedAt >= cutoff)
                .GroupBy(r => r.ReferrerUserId)
                .Select(g => new
                {
                    ReferrerUserId = g.Key,
                    TotalReferrals = g.Count(),
                    CompletedReferrals = g.Count(x => x.Status == ReferralStatus.Completed),
                    PendingReferrals = g.Count(x => x.Status == ReferralStatus.Pending),
                    TotalDiscountGiven = g.Sum(x => x.DiscountAmount),
                    TotalRewardEarned = g.Sum(x => x.RewardAmount)
                })
                .ToListAsync(cancellationToken);

            var userLookup = await _context.Users
                .AsNoTracking()
                .Where(u => grouped.Select(g => g.ReferrerUserId).Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

            var result = new List<ReferralAbuseSignalDto>();
            foreach (var row in grouped)
            {
                var signals = new List<string>();
                if (row.TotalReferrals >= 5) signals.Add("High referral volume in 30 days");
                if (row.CompletedReferrals >= 3) signals.Add("Multiple successful referral conversions");
                if (row.TotalDiscountGiven >= 2000m) signals.Add("High cumulative discount value");
                if (row.PendingReferrals >= 4) signals.Add("Large number of pending referrals");

                if (signals.Count == 0)
                {
                    continue;
                }

                result.Add(new ReferralAbuseSignalDto
                {
                    UserId = row.ReferrerUserId,
                    UserName = userLookup.TryGetValue(row.ReferrerUserId, out var name) ? name : "Unknown",
                    TotalReferrals = row.TotalReferrals,
                    CompletedReferrals = row.CompletedReferrals,
                    PendingReferrals = row.PendingReferrals,
                    TotalDiscountGiven = row.TotalDiscountGiven,
                    TotalRewardEarned = row.TotalRewardEarned,
                    RiskLevel = row.TotalReferrals >= 8 || row.TotalDiscountGiven >= 5000m ? "HIGH" : "MEDIUM",
                    Signals = signals
                });
            }

            return result
                .OrderByDescending(x => x.RiskLevel)
                .ThenByDescending(x => x.TotalReferrals)
                .ToList();
        }

        public async Task<int> GetTotalUsersCountAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AsNoTracking()
                .CountAsync(cancellationToken);
        }
    }
}
