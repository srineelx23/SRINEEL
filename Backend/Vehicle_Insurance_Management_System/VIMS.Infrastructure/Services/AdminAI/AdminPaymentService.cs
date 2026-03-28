using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Services.AdminAI
{
    public class AdminPaymentService : IAdminPaymentService
    {
        private readonly VehicleInsuranceContext _context;

        public AdminPaymentService(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<PaymentContextDto>> GetRecentPaymentsAsync(int take = 50, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 50 : take;
            return await _context.Payments
                .AsNoTracking()
                .OrderByDescending(p => p.PaymentDate)
                .Take(safeTake)
                .Select(Map())
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PaymentContextDto>> GetPaymentsByPolicyIdAsync(int policyId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .AsNoTracking()
                .Where(p => p.PolicyId == policyId)
                .OrderByDescending(p => p.PaymentDate)
                .Select(Map())
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PaymentContextDto>> GetPaymentsByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .AsNoTracking()
                .Where(p => p.Policy.CustomerId == userId)
                .OrderByDescending(p => p.PaymentDate)
                .Select(Map())
                .ToListAsync(cancellationToken);
        }

        public async Task<PaymentAggregateContextDto> GetPaymentAggregatesAsync(CancellationToken cancellationToken = default)
        {
            var paidPayments = _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == VIMS.Domain.Enums.PaymentStatus.Paid);

            var allPayments = _context.Payments.AsNoTracking();

            var totalPaidAmount = await paidPayments
                .Select(p => (decimal?)p.Amount)
                .SumAsync(cancellationToken) ?? 0m;

            var totalAmountAllStatuses = await allPayments
                .Select(p => (decimal?)p.Amount)
                .SumAsync(cancellationToken) ?? 0m;

            var paidPaymentsCount = await paidPayments.CountAsync(cancellationToken);
            var totalPaymentsCount = await allPayments.CountAsync(cancellationToken);

            return new PaymentAggregateContextDto
            {
                TotalPaidAmount = totalPaidAmount,
                TotalAmountAllStatuses = totalAmountAllStatuses,
                PaidPaymentsCount = paidPaymentsCount,
                TotalPaymentsCount = totalPaymentsCount
            };
        }

        private static System.Linq.Expressions.Expression<Func<VIMS.Domain.Entities.Payment, PaymentContextDto>> Map()
        {
            return p => new PaymentContextDto
            {
                PaymentId = p.PaymentId,
                PolicyId = p.PolicyId,
                PolicyNumber = p.Policy.PolicyNumber,
                CustomerId = p.Policy.CustomerId,
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                Status = p.Status.ToString(),
                PaymentMethod = p.PaymentMethod.ToString(),
                TransactionReference = p.TransactionReference
            };
        }
    }
}