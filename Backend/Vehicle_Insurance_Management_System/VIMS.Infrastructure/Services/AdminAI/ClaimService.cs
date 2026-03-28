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

        public async Task<IReadOnlyList<ClaimContextDto>> GetClaimsByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Claims
                .AsNoTracking()
                .Where(c => c.CustomerId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(MapToDtoExpression())
                .ToListAsync(cancellationToken);
        }

            public async Task<IReadOnlyList<ClaimContextDto>> GetRecentClaimsAsync(int take = 20, CancellationToken cancellationToken = default)
            {
                var safeTake = take <= 0 ? 20 : take;
                return await _context.Claims
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Take(safeTake)
                .Select(MapToDtoExpression())
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
