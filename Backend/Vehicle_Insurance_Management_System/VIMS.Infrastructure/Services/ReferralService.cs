using System.Data;
using Microsoft.EntityFrameworkCore;
using VIMS.Application.DTOs;
using VIMS.Application.Exceptions;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Services
{
    public class ReferralService : IReferralService
    {
        private const decimal DiscountPercentage = 0.05m;
        private const decimal MaxDiscountAmount = 1000m;
        private const decimal ReferrerRewardAmount = 300m;

        private readonly VehicleInsuranceContext _context;
        private readonly IUserRepository _userRepository;
        private readonly IReferralRepository _referralRepository;

        public ReferralService(
            VehicleInsuranceContext context,
            IUserRepository userRepository,
            IReferralRepository referralRepository)
        {
            _context = context;
            _userRepository = userRepository;
            _referralRepository = referralRepository;
        }

        public async Task ApplyReferralCodeAsync(int refereeUserId, string referralCode)
        {
            var code = (referralCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new BadRequestException("Referral code is required.");
            }

            var referee = await _userRepository.GetByIdAsync(refereeUserId);
            if (referee == null || referee.Role != UserRole.Customer)
            {
                throw new NotFoundException("Customer not found.");
            }

            if (referee.HasUsedReferral)
            {
                throw new BadRequestException("Referral has already been used for this account.");
            }

            if (referee.ReferredByUserId.HasValue)
            {
                throw new BadRequestException("Referral code has already been applied for this account.");
            }

            var referrer = await _userRepository.GetByReferralCodeAsync(code);
            if (referrer == null || referrer.Role != UserRole.Customer || !referrer.IsActive)
            {
                throw new NotFoundException("Referral code is invalid.");
            }

            if (referrer.UserId == refereeUserId)
            {
                throw new BadRequestException("You cannot use your own referral code.");
            }

            referee.ReferredByUserId = referrer.UserId;
            await _userRepository.UpdateAsync(referee);

            var existingReferral = await _referralRepository.GetByRefereeUserIdAsync(refereeUserId);
            if (existingReferral == null)
            {
                await _referralRepository.AddAsync(new Referral
                {
                    ReferrerUserId = referrer.UserId,
                    RefereeUserId = referee.UserId,
                    Status = ReferralStatus.Pending,
                    DiscountAmount = 0,
                    RewardAmount = 0,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        public async Task<ReferralDiscountPreviewDTO> GetDiscountPreviewForQuoteAsync(int refereeUserId, decimal baseAmount)
        {
            return await BuildDiscountPreviewAsync(refereeUserId, baseAmount);
        }

        public async Task<ReferralDiscountPreviewDTO> GetDiscountPreviewAsync(int refereeUserId, int policyId, decimal baseAmount)
        {
            var policyOwned = await _context.Policies.AnyAsync(p => p.PolicyId == policyId && p.CustomerId == refereeUserId);
            if (!policyOwned)
            {
                return new ReferralDiscountPreviewDTO
                {
                    BaseAmount = baseAmount,
                    DiscountAmount = 0,
                    FinalAmount = baseAmount,
                    IsEligible = false,
                    Reason = "Referral discount is not available."
                };
            }

            return await BuildDiscountPreviewAsync(refereeUserId, baseAmount);
        }

        private async Task<ReferralDiscountPreviewDTO> BuildDiscountPreviewAsync(int refereeUserId, decimal baseAmount)
        {
            var preview = new ReferralDiscountPreviewDTO
            {
                BaseAmount = baseAmount,
                DiscountAmount = 0,
                FinalAmount = baseAmount,
                IsEligible = false,
                Reason = "Referral discount is not available."
            };

            var referee = await _context.Users.FirstOrDefaultAsync(u => u.UserId == refereeUserId);
            if (referee == null || referee.Role != UserRole.Customer)
            {
                return preview;
            }

            if (referee.HasUsedReferral || !referee.ReferredByUserId.HasValue)
            {
                preview.Reason = "No active referral linked to this account.";
                return preview;
            }

            var hasAnyPaidPremium = await _context.Payments
                .Include(p => p.Policy)
                .AnyAsync(p => p.Policy.CustomerId == refereeUserId
                               && p.Status == PaymentStatus.Paid
                               && p.Amount > 0
                               && !EF.Functions.Like(p.TransactionReference ?? string.Empty, "%Transfer%")
                               && !EF.Functions.Like(p.TransactionReference ?? string.Empty, "%Claim%"));

            if (hasAnyPaidPremium)
            {
                preview.Reason = "Referral discount is only valid on your first successful premium payment.";
                return preview;
            }

            var referral = await _context.Referrals.FirstOrDefaultAsync(r => r.RefereeUserId == refereeUserId);
            if (referral != null && referral.Status == ReferralStatus.Completed)
            {
                preview.Reason = "Referral reward already processed.";
                return preview;
            }

            var discount = Math.Min(Math.Round(baseAmount * DiscountPercentage, 2), MaxDiscountAmount);
            preview.IsEligible = discount > 0;
            preview.DiscountAmount = discount;
            preview.FinalAmount = Math.Max(0, baseAmount - discount);
            preview.Reason = preview.IsEligible ? "Referral discount applied." : preview.Reason;
            return preview;
        }

        public async Task ProcessRewardAfterPaymentAsync(int refereeUserId, int policyId, decimal discountApplied)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var referee = await _context.Users.FirstOrDefaultAsync(u => u.UserId == refereeUserId);
            if (referee == null || !referee.ReferredByUserId.HasValue || referee.HasUsedReferral)
            {
                await tx.CommitAsync();
                return;
            }

            var hasPolicy = await _context.Policies.AnyAsync(p => p.PolicyId == policyId && p.CustomerId == refereeUserId);
            if (!hasPolicy)
            {
                throw new BadRequestException("Invalid policy for referral completion.");
            }

            var referral = await _context.Referrals.FirstOrDefaultAsync(r => r.RefereeUserId == refereeUserId);
            if (referral == null)
            {
                referral = new Referral
                {
                    ReferrerUserId = referee.ReferredByUserId.Value,
                    RefereeUserId = referee.UserId,
                    Status = ReferralStatus.Pending,
                    DiscountAmount = 0,
                    RewardAmount = 0,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.Referrals.AddAsync(referral);
                await _context.SaveChangesAsync();
            }

            if (referral.Status == ReferralStatus.Completed)
            {
                await tx.CommitAsync();
                return;
            }

            var paidPremiumCount = await _context.Payments
                .Include(p => p.Policy)
                .CountAsync(p => p.Policy.CustomerId == refereeUserId
                                 && p.Status == PaymentStatus.Paid
                                 && p.Amount > 0
                                 && !EF.Functions.Like(p.TransactionReference ?? string.Empty, "%Transfer%")
                                 && !EF.Functions.Like(p.TransactionReference ?? string.Empty, "%Claim%"));

            if (paidPremiumCount != 1)
            {
                await tx.CommitAsync();
                return;
            }

            referee.HasUsedReferral = true;
            referral.Status = ReferralStatus.Completed;
            referral.DiscountAmount = Math.Max(0, discountApplied);
            referral.RewardAmount = ReferrerRewardAmount;
            referral.CompletedAt = DateTime.UtcNow;

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == referral.ReferrerUserId);
            if (wallet == null)
            {
                wallet = new Wallet
                {
                    UserId = referral.ReferrerUserId,
                    Balance = ReferrerRewardAmount,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.Wallets.AddAsync(wallet);
            }
            else
            {
                wallet.Balance += ReferrerRewardAmount;
                wallet.UpdatedAt = DateTime.UtcNow;
                _context.Wallets.Update(wallet);
            }

            _context.Users.Update(referee);
            _context.Referrals.Update(referral);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task<List<object>> GetReferralHistoryAsync(int userId)
        {
            var referredUsers = await _context.Referrals
                .Include(r => r.RefereeUser)
                .Where(r => r.ReferrerUserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    referralId = r.ReferralId,
                    refereeUserId = r.RefereeUserId,
                    refereeName = r.RefereeUser.FullName,
                    status = r.Status.ToString(),
                    discountAmount = r.DiscountAmount,
                    rewardAmount = r.RewardAmount,
                    createdAt = r.CreatedAt,
                    completedAt = r.CompletedAt
                })
                .Cast<object>()
                .ToListAsync();

            return referredUsers;
        }

        public async Task<IReadOnlyList<ReferralContextDto>> GetAllReferralsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Referrals
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReferralContextDto
                {
                    ReferralId = r.ReferralId,
                    ReferrerUserId = r.ReferrerUserId,
                    RefereeUserId = r.RefereeUserId,
                    DiscountAmount = r.DiscountAmount,
                    RewardAmount = r.RewardAmount,
                    Status = r.Status.ToString(),
                    CreatedAt = r.CreatedAt,
                    CompletedAt = r.CompletedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ReferralAbuseSignalDto>> GetReferralAbuseSignalsAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow.AddDays(-30);

            var grouped = await _context.Referrals
                .AsNoTracking()
                .Where(r => r.CreatedAt >= cutoff)
                .GroupBy(r => r.ReferrerUserId)
                .Select(g => new
                {
                    ReferrerUserId = g.Key,
                    TotalReferrals = g.Count(),
                    CompletedReferrals = g.Count(x => x.Status == ReferralStatus.Completed),
                    PendingReferrals = g.Count(x => x.Status == ReferralStatus.Pending),
                    TotalDiscountGiven = g.Sum(x => x.DiscountAmount),
                    TotalRewardEarned = g.Sum(x => x.RewardAmount)
                })
                .ToListAsync(cancellationToken);

            var userMap = await _context.Users
                .AsNoTracking()
                .Where(u => grouped.Select(g => g.ReferrerUserId).Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

            var result = new List<ReferralAbuseSignalDto>();
            foreach (var row in grouped)
            {
                var signals = new List<string>();
                if (row.TotalReferrals >= 5) signals.Add("High referral volume in 30 days");
                if (row.CompletedReferrals >= 3) signals.Add("Multiple successful referral conversions");
                if (row.TotalDiscountGiven >= 2000m) signals.Add("High cumulative discount value");
                if (row.PendingReferrals >= 4) signals.Add("Large number of pending referrals");

                if (signals.Count == 0)
                {
                    continue;
                }

                result.Add(new ReferralAbuseSignalDto
                {
                    UserId = row.ReferrerUserId,
                    UserName = userMap.TryGetValue(row.ReferrerUserId, out var name) ? name : "Unknown",
                    TotalReferrals = row.TotalReferrals,
                    CompletedReferrals = row.CompletedReferrals,
                    PendingReferrals = row.PendingReferrals,
                    TotalDiscountGiven = row.TotalDiscountGiven,
                    TotalRewardEarned = row.TotalRewardEarned,
                    RiskLevel = row.TotalReferrals >= 8 || row.TotalDiscountGiven >= 5000m ? "HIGH" : "MEDIUM",
                    Signals = signals
                });
            }

            return result
                .OrderByDescending(x => x.RiskLevel)
                .ThenByDescending(x => x.TotalReferrals)
                .ToList();
        }
    }
}
