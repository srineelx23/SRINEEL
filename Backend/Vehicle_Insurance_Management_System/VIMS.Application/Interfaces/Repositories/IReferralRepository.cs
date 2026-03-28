using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IReferralRepository
    {
        Task<Referral?> GetByRefereeUserIdAsync(int refereeUserId);
        Task<List<Referral>> GetByReferrerUserIdAsync(int referrerUserId);
        Task AddAsync(Referral referral);
        Task UpdateAsync(Referral referral);
    }
}
