using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;
using VIMS.Domain.Enums;
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
            var premiumPayments = _context.Payments
                .AsNoTracking()
                .Where(p => p.PaymentType == PaymentType.Premium);

            var paidPremiumPayments = premiumPayments
                .Where(p => p.Status == PaymentStatus.Paid);

            var paidClaimPayouts = _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == PaymentStatus.Paid && p.PaymentType == PaymentType.ClaimPayout);

            var paidTransferFees = _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == PaymentStatus.Paid && p.PaymentType == PaymentType.TransferFee);

            var totalPremiumPaidAmount = await paidPremiumPayments
                .Select(p => (decimal?)p.Amount)
                .SumAsync(cancellationToken) ?? 0m;

            var totalPremiumAllStatuses = await premiumPayments
                .Select(p => (decimal?)p.Amount)
                .SumAsync(cancellationToken) ?? 0m;

            var totalClaimPayoutAmount = await paidClaimPayouts
                .Select(p => (decimal?)p.Amount)
                .SumAsync(cancellationToken) ?? 0m;

            var totalTransferFeeAmount = await paidTransferFees
                .Select(p => (decimal?)p.Amount)
                .SumAsync(cancellationToken) ?? 0m;

            var paidPremiumPaymentsCount = await paidPremiumPayments.CountAsync(cancellationToken);
            var totalPremiumPaymentsCount = await premiumPayments.CountAsync(cancellationToken);
            var claimPayoutCount = await paidClaimPayouts.CountAsync(cancellationToken);
            var transferFeeCount = await paidTransferFees.CountAsync(cancellationToken);

            return new PaymentAggregateContextDto
            {
                TotalPaidAmount = totalPremiumPaidAmount,
                TotalAmountAllStatuses = totalPremiumAllStatuses,
                PaidPaymentsCount = paidPremiumPaymentsCount,
                TotalPaymentsCount = totalPremiumPaymentsCount,
                TotalClaimPayoutAmount = totalClaimPayoutAmount,
                ClaimPayoutCount = claimPayoutCount,
                TotalTransferFeeAmount = totalTransferFeeAmount,
                TransferFeeCount = transferFeeCount
            };
        }

        public async Task<decimal> GetTotalPaymentAmountAsync(PaymentStatus? statusFilter = null, int? userId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Payments
                .AsNoTracking()
                .Where(p => p.PaymentType == PaymentType.Premium)
                .AsQueryable();

            if (statusFilter.HasValue)
            {
                query = query.Where(p => p.Status == statusFilter.Value);
            }
            else
            {
                query = query.Where(p => p.Status == PaymentStatus.Paid);
            }

            if (userId.HasValue)
            {
                query = query.Where(p => p.Policy.CustomerId == userId.Value);
            }

            return await query.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
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
                PaymentType = p.PaymentType.ToString(),
                TransactionReference = p.TransactionReference
            };
        }
    }
}