namespace VIMS.Application.Interfaces.Services
{
    public interface IWalletService
    {
        Task<decimal> GetBalanceAsync(int userId);
        Task CreditAsync(int userId, decimal amount);
    }
}
