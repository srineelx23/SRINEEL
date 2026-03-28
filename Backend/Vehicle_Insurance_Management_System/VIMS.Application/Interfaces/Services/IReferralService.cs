using VIMS.Application.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IReferralService
    {
        Task ApplyReferralCodeAsync(int refereeUserId, string referralCode);
        Task<ReferralDiscountPreviewDTO> GetDiscountPreviewForQuoteAsync(int refereeUserId, decimal baseAmount);
        Task<ReferralDiscountPreviewDTO> GetDiscountPreviewAsync(int refereeUserId, int policyId, decimal baseAmount);
        Task ProcessRewardAfterPaymentAsync(int refereeUserId, int policyId, decimal discountApplied);
        Task<List<object>> GetReferralHistoryAsync(int userId);
        Task<IReadOnlyList<VIMS.Domain.DTOs.ReferralContextDto>> GetAllReferralsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VIMS.Domain.DTOs.ReferralAbuseSignalDto>> GetReferralAbuseSignalsAsync(CancellationToken cancellationToken = default);
    }
}
