using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IWalletRepository
    {
        Task<Wallet?> GetByUserIdAsync(int userId);
        Task AddAsync(Wallet wallet);
        Task UpdateAsync(Wallet wallet);
    }
}
