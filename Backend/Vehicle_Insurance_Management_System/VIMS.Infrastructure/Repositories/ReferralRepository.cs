using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Entities;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Repositories
{
    public class ReferralRepository : IReferralRepository
    {
        private readonly VehicleInsuranceContext _context;

        public ReferralRepository(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<Referral?> GetByRefereeUserIdAsync(int refereeUserId)
        {
            return await _context.Referrals
                .Include(r => r.ReferrerUser)
                .Include(r => r.RefereeUser)
                .FirstOrDefaultAsync(r => r.RefereeUserId == refereeUserId);
        }

        public async Task<List<Referral>> GetByReferrerUserIdAsync(int referrerUserId)
        {
            return await _context.Referrals
                .Include(r => r.RefereeUser)
                .Where(r => r.ReferrerUserId == referrerUserId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(Referral referral)
        {
            await _context.Referrals.AddAsync(referral);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Referral referral)
        {
            _context.Referrals.Update(referral);
            await _context.SaveChangesAsync();
        }
    }
}
