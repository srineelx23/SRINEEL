using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IAdminPaymentService
    {
        Task<IReadOnlyList<PaymentContextDto>> GetRecentPaymentsAsync(int take = 50, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PaymentContextDto>> GetPaymentsByPolicyIdAsync(int policyId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PaymentContextDto>> GetPaymentsByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<PaymentAggregateContextDto> GetPaymentAggregatesAsync(CancellationToken cancellationToken = default);
    }
}