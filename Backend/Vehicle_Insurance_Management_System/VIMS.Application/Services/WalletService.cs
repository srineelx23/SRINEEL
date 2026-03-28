using VIMS.Application.Exceptions;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;

namespace VIMS.Application.Services
{
    public class WalletService : IWalletService
    {
        private readonly IWalletRepository _walletRepository;

        public WalletService(IWalletRepository walletRepository)
        {
            _walletRepository = walletRepository;
        }

        public async Task<decimal> GetBalanceAsync(int userId)
        {
            var wallet = await _walletRepository.GetByUserIdAsync(userId);
            return wallet?.Balance ?? 0m;
        }

        public async Task CreditAsync(int userId, decimal amount)
        {
            if (amount <= 0)
            {
                throw new BadRequestException("Credit amount must be greater than zero.");
            }

            var wallet = await _walletRepository.GetByUserIdAsync(userId);
            if (wallet == null)
            {
                wallet = new Wallet
                {
                    UserId = userId,
                    Balance = amount,
                    UpdatedAt = DateTime.UtcNow
                };
                await _walletRepository.AddAsync(wallet);
                return;
            }

            wallet.Balance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            await _walletRepository.UpdateAsync(wallet);
        }
    }
}
