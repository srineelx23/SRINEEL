using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Entities;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Repositories
{
    public class ClaimsRepository : IClaimsRepository
    {
        private readonly VehicleInsuranceContext _context;
        public ClaimsRepository(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<List<Claims>> GetAllAsync()
        {
            return await _context.Claims
                .Include(c => c.Documents)
                .Include(c => c.Customer)
                .Include(c => c.ClaimsOfficer)
                .Include(c => c.Policy)
                    .ThenInclude(p => p.Plan)
                .Include(c => c.Policy)
                    .ThenInclude(p => p.Vehicle)
                .ToListAsync();
        }

        public async Task<bool> ExistsActiveClaimForPolicyAsync(int policyId)
        {
            // Active means Submitted and not yet Rejected/Approved
            return await _context.Claims.AnyAsync(c => c.PolicyId == policyId && c.Status == VIMS.Domain.Enums.ClaimStatus.Submitted);
        }

        public async Task<Claims> AddAsync(Claims claim)
        {
            await _context.Claims.AddAsync(claim);
            await _context.SaveChangesAsync();
            return claim;
        }

        public async Task AddDocumentAsync(ClaimDocument doc)
        {
            await _context.ClaimDocuments.AddAsync(doc);
            await _context.SaveChangesAsync();
        }

        public async Task<Claims?> GetByIdAsync(int claimId)
        {
            return await _context.Claims
                .Include(c => c.Documents)
                .Include(c => c.Customer)
                    .ThenInclude(cust => cust.CustomerClaims)
                .Include(c => c.ClaimsOfficer)
                .Include(c => c.Policy)
                    .ThenInclude(p => p.Plan)
                .Include(c => c.Policy)
                    .ThenInclude(p => p.Payments)
                .Include(c => c.Policy)
                    .ThenInclude(p => p.Vehicle)
                        .ThenInclude(v => v.VehicleApplication)
                            .ThenInclude(va => va.Documents)
                .FirstOrDefaultAsync(c => c.ClaimId == claimId);
        }

        public async Task<List<Claims>> GetByOfficerIdAsync(int officerId)
        {
            return await _context.Claims
                .Where(c => c.ClaimsOfficerId == officerId)
                .Include(c => c.Documents)
                .Include(c => c.Customer)
                .Include(c => c.ClaimsOfficer)
                .Include(c => c.Policy)
                    .ThenInclude(p => p.Plan)
                .Include(c => c.Policy)
                    .ThenInclude(p => p.Vehicle)
                        .ThenInclude(v => v.VehicleApplication)
                            .ThenInclude(va => va.Documents)
                .ToListAsync();
        }

        public async Task UpdateAsync(Claims claim)
        {
            _context.Claims.Update(claim);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Claims>> GetByCustomerIdAsync(int customerId)
        {
            return await _context.Claims
                .Where(c => c.CustomerId == customerId)
                .Include(c => c.Documents)
                .Include(c => c.Policy)
                    .ThenInclude(p => p.Vehicle)
                .ToListAsync();
        }

        public async Task<List<Claims>> GetByCustomerIdsAsync(IReadOnlyCollection<int> customerIds)
        {
            if (customerIds.Count == 0)
            {
                return new List<Claims>();
            }

            return await _context.Claims
                .Where(c => customerIds.Contains(c.CustomerId))
                .Include(c => c.Documents)
                .Include(c => c.Customer)
                .Include(c => c.ClaimsOfficer)
                .Include(c => c.Policy)
                    .ThenInclude(p => p.Plan)
                .Include(c => c.Policy)
                    .ThenInclude(p => p.Vehicle)
                .ToListAsync();
        }

        public async Task<bool> HasAnyClaimsAsync(int policyId)
        {
            return await _context.Claims.AnyAsync(c => c.PolicyId == policyId);
        }
    }
}
