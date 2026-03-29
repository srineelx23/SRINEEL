using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;
using VIMS.Domain.Enums;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Services.AdminAI
{
    public class ClaimService : IClaimService
    {
        private readonly VehicleInsuranceContext _context;

        public ClaimService(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<ClaimContextDto?> GetClaimByIdAsync(int claimId, CancellationToken cancellationToken = default)
        {
            return await _context.Claims
                .AsNoTracking()
                .Where(c => c.ClaimId == claimId)
                .Select(MapToDtoExpression())
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<ClaimContextDto?> GetClaimByNumberAsync(string claimNumber, CancellationToken cancellationToken = default)
        {
            var normalized = (claimNumber ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            return await _context.Claims
                .AsNoTracking()
                .Where(c => c.ClaimNumber.ToUpper() == normalized)
                .Select(MapToDtoExpression())
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<(int UserId, int ClaimCount)?> GetUserWithMostClaimsAsync(CancellationToken cancellationToken = default)
        {
            var topUser = await _context.Claims
                .AsNoTracking()
                .GroupBy(c => c.CustomerId)
                .Select(g => new { UserId = g.Key, ClaimCount = g.Count() })
                .OrderByDescending(x => x.ClaimCount)
                .ThenBy(x => x.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            return topUser == null ? null : (topUser.UserId, topUser.ClaimCount);
        }

        public async Task<(int PolicyId, int ClaimCount)?> GetPolicyWithMostClaimsAsync(CancellationToken cancellationToken = default)
        {
            var topPolicy = await _context.Claims
                .AsNoTracking()
                .GroupBy(c => c.PolicyId)
                .Select(g => new { PolicyId = g.Key, ClaimCount = g.Count() })
                .OrderByDescending(x => x.ClaimCount)
                .ThenBy(x => x.PolicyId)
                .FirstOrDefaultAsync(cancellationToken);

            return topPolicy == null ? null : (topPolicy.PolicyId, topPolicy.ClaimCount);
        }

        public async Task<ClaimContextDto?> GetHighestPayoutClaimAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Claims
                .AsNoTracking()
                .Where(c => c.ApprovedAmount.HasValue)
                .OrderByDescending(c => c.ApprovedAmount)
                .ThenByDescending(c => c.CreatedAt)
                .Select(MapToDtoExpression())
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ClaimContextDto>> GetClaimsByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Claims
                .AsNoTracking()
                .Where(c => c.CustomerId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(MapToDtoExpression())
                .ToListAsync(cancellationToken);
        }

            public async Task<IReadOnlyList<ClaimContextDto>> GetRejectedClaimsForDateAsync(DateTime utcDate, int take = 10, CancellationToken cancellationToken = default)
            {
                var safeTake = take <= 0 ? 10 : Math.Min(take, 10);
                var from = utcDate.Date;
                var to = from.AddDays(1);

                return await _context.Claims
                .AsNoTracking()
                .Where(c => c.Status == ClaimStatus.Rejected)
                .Where(c => c.CreatedAt >= from && c.CreatedAt < to)
                .OrderByDescending(c => c.CreatedAt)
                .Take(safeTake)
                .Select(MapToDtoExpression())
                .ToListAsync(cancellationToken);
            }

        public async Task<IReadOnlyList<ClaimContextDto>> GetRelevantClaimsAsync(bool rejectedOnly = false, int take = 10, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 10 : Math.Min(take, 10);

            var query = _context.Claims
                .AsNoTracking()
                .AsQueryable();

            if (rejectedOnly)
            {
                query = query.Where(c => c.Status == ClaimStatus.Rejected);
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Take(safeTake)
                .Select(c => new ClaimContextDto
                {
                    ClaimId = c.ClaimId,
                    ClaimNumber = c.ClaimNumber,
                    PolicyId = c.PolicyId,
                    CustomerId = c.CustomerId,
                    Status = c.Status.ToString(),
                    ApprovedAmount = c.ApprovedAmount,
                    DecisionType = c.DecisionType,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ClaimContextDto>> GetClaimsForAnalyticsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Claims
                .AsNoTracking()
                .Select(c => new ClaimContextDto
                {
                    ClaimId = c.ClaimId,
                    PolicyId = c.PolicyId,
                    Status = c.Status.ToString(),
                    ApprovedAmount = c.ApprovedAmount
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ClaimContextDto>> GetRecentRejectedClaimsAsync(int take = 10, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 10 : take;
            return await _context.Claims
                .AsNoTracking()
                .Where(c => c.Status == ClaimStatus.Rejected)
                .OrderByDescending(c => c.CreatedAt)
                .Take(safeTake)
                .Select(MapToDtoExpression())
                .ToListAsync(cancellationToken);
        }

            public async Task<int> GetTotalClaimsCountAsync(CancellationToken cancellationToken = default)
            {
                return await _context.Claims
                .AsNoTracking()
                .CountAsync(cancellationToken);
            }

        private static System.Linq.Expressions.Expression<Func<VIMS.Domain.Entities.Claims, ClaimContextDto>> MapToDtoExpression()
        {
            return c => new ClaimContextDto
            {
                ClaimId = c.ClaimId,
                ClaimNumber = c.ClaimNumber,
                PolicyId = c.PolicyId,
                CustomerId = c.CustomerId,
                ClaimsOfficerId = c.ClaimsOfficerId,
                ClaimType = c.claimType.ToString(),
                Status = c.Status.ToString(),
                ApprovedAmount = c.ApprovedAmount,
                DecisionType = c.DecisionType,
                RejectionReason = c.RejectionReason,
                CreatedAt = c.CreatedAt
            };
        }
    }
}
