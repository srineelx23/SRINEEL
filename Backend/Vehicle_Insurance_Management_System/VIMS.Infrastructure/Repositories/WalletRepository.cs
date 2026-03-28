using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Entities;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Repositories
{
    public class WalletRepository : IWalletRepository
    {
        private readonly VehicleInsuranceContext _context;

        public WalletRepository(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<Wallet?> GetByUserIdAsync(int userId)
        {
            return await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        }

        public async Task AddAsync(Wallet wallet)
        {
            await _context.Wallets.AddAsync(wallet);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Wallet wallet)
        {
            _context.Wallets.Update(wallet);
            await _context.SaveChangesAsync();
        }
    }
}
