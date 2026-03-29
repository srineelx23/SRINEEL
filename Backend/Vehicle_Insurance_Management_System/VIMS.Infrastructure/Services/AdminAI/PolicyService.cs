using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;
using VIMS.Domain.Enums;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Services.AdminAI
{
    public class PolicyService : IPolicyService
    {
        private readonly VehicleInsuranceContext _context;

        public PolicyService(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<PolicyContextDto?> GetPolicyByIdAsync(int policyId, CancellationToken cancellationToken = default)
        {
            return await _context.Policies
                .AsNoTracking()
                .Where(p => p.PolicyId == policyId)
                .Select(p => new PolicyContextDto
                {
                    PolicyId = p.PolicyId,
                    PolicyNumber = p.PolicyNumber,
                    CustomerId = p.CustomerId,
                    CustomerName = p.Customer.FullName,
                    VehicleId = p.VehicleId,
                    PlanId = p.PlanId,
                    Status = p.Status.ToString(),
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    PremiumAmount = p.PremiumAmount,
                    InvoiceAmount = p.InvoiceAmount,
                    IDV = p.IDV,
                    ClaimCount = p.ClaimCount,
                    PlanName = p.Plan.PlanName,
                    VehicleRegistrationNumber = p.Vehicle.RegistrationNumber,
                    VehicleMake = p.Vehicle.Make,
                    VehicleModel = p.Vehicle.Model,
                    VehicleYear = p.Vehicle.Year,
                    VehicleType = p.Vehicle.VehicleType,
                    FuelType = p.Vehicle.FuelType
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PolicyContextDto>> GetPoliciesByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Policies
                .AsNoTracking()
                .Where(p => p.CustomerId == userId)
                .OrderByDescending(p => p.StartDate)
                .Select(p => new PolicyContextDto
                {
                    PolicyId = p.PolicyId,
                    PolicyNumber = p.PolicyNumber,
                    CustomerId = p.CustomerId,
                    CustomerName = p.Customer.FullName,
                    VehicleId = p.VehicleId,
                    PlanId = p.PlanId,
                    Status = p.Status.ToString(),
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    PremiumAmount = p.PremiumAmount,
                    InvoiceAmount = p.InvoiceAmount,
                    IDV = p.IDV,
                    ClaimCount = p.ClaimCount,
                    PlanName = p.Plan.PlanName,
                    VehicleRegistrationNumber = p.Vehicle.RegistrationNumber,
                    VehicleMake = p.Vehicle.Make,
                    VehicleModel = p.Vehicle.Model,
                    VehicleYear = p.Vehicle.Year,
                    VehicleType = p.Vehicle.VehicleType,
                    FuelType = p.Vehicle.FuelType
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PolicyContextDto>> GetPoliciesExpiringInRangeAsync(DateTime fromInclusiveUtc, DateTime toInclusiveUtc, int take = 10, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 10 : Math.Min(take, 10);
            var fromDate = fromInclusiveUtc.Date;
            var toDate = toInclusiveUtc.Date;

            return await _context.Policies
                .AsNoTracking()
                .Where(p => p.EndDate.Date >= fromDate && p.EndDate.Date <= toDate)
                .OrderBy(p => p.EndDate)
                .ThenByDescending(p => p.PolicyId)
                .Take(safeTake)
                .Select(p => new PolicyContextDto
                {
                    PolicyId = p.PolicyId,
                    PolicyNumber = p.PolicyNumber,
                    CustomerId = p.CustomerId,
                    CustomerName = p.Customer.FullName,
                    VehicleId = p.VehicleId,
                    PlanId = p.PlanId,
                    Status = p.Status.ToString(),
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    PremiumAmount = p.PremiumAmount,
                    InvoiceAmount = p.InvoiceAmount,
                    IDV = p.IDV,
                    ClaimCount = p.ClaimCount,
                    PlanName = p.Plan.PlanName,
                    VehicleRegistrationNumber = p.Vehicle.RegistrationNumber,
                    VehicleMake = p.Vehicle.Make,
                    VehicleModel = p.Vehicle.Model,
                    VehicleYear = p.Vehicle.Year,
                    VehicleType = p.Vehicle.VehicleType,
                    FuelType = p.Vehicle.FuelType
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PolicyContextDto>> GetZeroDepreciationPoliciesWithVehiclesAsync(int take = 10, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 10 : Math.Min(take, 10);

            return await _context.Policies
                .AsNoTracking()
                .Where(p => p.Plan.ZeroDepreciationAvailable || p.Plan.PolicyType.ToLower().Contains("zerodepreciation") || p.Plan.PlanName.ToLower().Contains("zero depreciation"))
                .OrderByDescending(p => p.StartDate)
                .ThenByDescending(p => p.PolicyId)
                .Take(safeTake)
                .Select(p => new PolicyContextDto
                {
                    PolicyId = p.PolicyId,
                    PolicyNumber = p.PolicyNumber,
                    CustomerId = p.CustomerId,
                    CustomerName = p.Customer.FullName,
                    VehicleId = p.VehicleId,
                    PlanId = p.PlanId,
                    Status = p.Status.ToString(),
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    PremiumAmount = p.PremiumAmount,
                    InvoiceAmount = p.InvoiceAmount,
                    IDV = p.IDV,
                    ClaimCount = p.ClaimCount,
                    PlanName = p.Plan.PlanName,
                    VehicleRegistrationNumber = p.Vehicle.RegistrationNumber,
                    VehicleMake = p.Vehicle.Make,
                    VehicleModel = p.Vehicle.Model,
                    VehicleYear = p.Vehicle.Year,
                    VehicleType = p.Vehicle.VehicleType,
                    FuelType = p.Vehicle.FuelType
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<decimal> GetTotalPremiumAmountAsync(int? userId = null, bool pendingPaymentOnly = false, CancellationToken cancellationToken = default)
        {
            var query = _context.Policies
                .AsNoTracking()
                .AsQueryable();

            if (userId.HasValue)
            {
                query = query.Where(p => p.CustomerId == userId.Value);
            }

            if (pendingPaymentOnly)
            {
                query = query.Where(p => p.Status == PolicyStatus.PendingPayment);
            }

            return await query.SumAsync(p => (decimal?)p.PremiumAmount, cancellationToken) ?? 0m;
        }

        public async Task<IReadOnlyList<PolicyContextDto>> GetRelevantPoliciesAsync(bool pendingPaymentOnly = false, bool highestIdvFirst = false, int take = 10, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 10 : Math.Min(take, 10);

            var query = _context.Policies
                .AsNoTracking()
                .AsQueryable();

            if (pendingPaymentOnly)
            {
                query = query.Where(p => p.Status == PolicyStatus.PendingPayment);
            }

            query = highestIdvFirst
                ? query.OrderByDescending(p => p.IDV).ThenByDescending(p => p.PolicyId)
                : query.OrderByDescending(p => p.StartDate);

            return await query
                .Take(safeTake)
                .Select(p => new PolicyContextDto
                {
                    PolicyId = p.PolicyId,
                    PolicyNumber = p.PolicyNumber,
                    CustomerId = p.CustomerId,
                    CustomerName = p.Customer.FullName,
                    VehicleId = p.VehicleId,
                    Status = p.Status.ToString(),
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    PremiumAmount = p.PremiumAmount,
                    IDV = p.IDV,
                    PlanName = p.Plan.PlanName,
                    VehicleRegistrationNumber = p.Vehicle.RegistrationNumber,
                    VehicleType = p.Vehicle.VehicleType,
                    FuelType = p.Vehicle.FuelType
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PolicyContextDto>> GetPendingPaymentPoliciesAsync(int take = 200, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 200 : take;
            return await _context.Policies
                .AsNoTracking()
                .Where(p => p.Status == PolicyStatus.PendingPayment)
                .OrderByDescending(p => p.StartDate)
                .Take(safeTake)
                .Select(p => new PolicyContextDto
                {
                    PolicyId = p.PolicyId,
                    PolicyNumber = p.PolicyNumber,
                    CustomerId = p.CustomerId,
                    CustomerName = p.Customer.FullName,
                    VehicleId = p.VehicleId,
                    PlanId = p.PlanId,
                    Status = p.Status.ToString(),
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    PremiumAmount = p.PremiumAmount,
                    InvoiceAmount = p.InvoiceAmount,
                    IDV = p.IDV,
                    ClaimCount = p.ClaimCount,
                    PlanName = p.Plan.PlanName,
                    VehicleRegistrationNumber = p.Vehicle.RegistrationNumber,
                    VehicleMake = p.Vehicle.Make,
                    VehicleModel = p.Vehicle.Model,
                    VehicleYear = p.Vehicle.Year,
                    VehicleType = p.Vehicle.VehicleType,
                    FuelType = p.Vehicle.FuelType
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<PolicyContextDto?> GetPolicyWithHighestIdvAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Policies
                .AsNoTracking()
                .Where(p => p.Status != PolicyStatus.Draft)
                .OrderByDescending(p => p.IDV)
                .ThenByDescending(p => p.PolicyId)
                .Select(p => new PolicyContextDto
                {
                    PolicyId = p.PolicyId,
                    PolicyNumber = p.PolicyNumber,
                    CustomerId = p.CustomerId,
                    CustomerName = p.Customer.FullName,
                    VehicleId = p.VehicleId,
                    PlanId = p.PlanId,
                    Status = p.Status.ToString(),
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    PremiumAmount = p.PremiumAmount,
                    InvoiceAmount = p.InvoiceAmount,
                    IDV = p.IDV,
                    ClaimCount = p.ClaimCount,
                    PlanName = p.Plan.PlanName,
                    VehicleRegistrationNumber = p.Vehicle.RegistrationNumber,
                    VehicleMake = p.Vehicle.Make,
                    VehicleModel = p.Vehicle.Model,
                    VehicleYear = p.Vehicle.Year,
                    VehicleType = p.Vehicle.VehicleType,
                    FuelType = p.Vehicle.FuelType
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PolicyContextDto?> GetPolicyWithHighestIdvByFiltersAsync(string? vehicleType, string? fuelType, string? planType, CancellationToken cancellationToken = default)
        {
            var query = _context.Policies
                .AsNoTracking()
                .Where(p => p.Status != PolicyStatus.Draft)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(vehicleType))
            {
                var vehicleTypeFilter = vehicleType.Trim().ToLowerInvariant();

                if (vehicleTypeFilter == "car")
                {
                    query = query.Where(p =>
                        p.Vehicle.VehicleType.ToLower().Contains("car") ||
                        p.Vehicle.VehicleType.ToLower().Contains("private") ||
                        p.Vehicle.VehicleType.ToLower().Contains("four"));
                }
                else if (vehicleTypeFilter == "private")
                {
                    query = query.Where(p =>
                        p.Vehicle.VehicleType.ToLower().Contains("private") ||
                        p.Vehicle.VehicleType.ToLower().Contains("car") ||
                        p.Vehicle.VehicleType.ToLower().Contains("four"));
                }
                else
                {
                    query = query.Where(p => p.Vehicle.VehicleType.ToLower().Contains(vehicleTypeFilter));
                }
            }

            if (!string.IsNullOrWhiteSpace(fuelType))
            {
                var fuelTypeFilter = fuelType.Trim().ToLowerInvariant();

                if (fuelTypeFilter == "electric")
                {
                    query = query.Where(p =>
                        p.Vehicle.FuelType.ToLower().Contains("electric") ||
                        p.Vehicle.FuelType.ToLower().Contains("ev"));
                }
                else
                {
                    query = query.Where(p => p.Vehicle.FuelType.ToLower().Contains(fuelTypeFilter));
                }
            }

            if (!string.IsNullOrWhiteSpace(planType))
            {
                var planTypeFilter = planType.Trim().ToLowerInvariant();
                query = query.Where(p => p.Plan.PolicyType.ToLower().Contains(planTypeFilter) || p.Plan.PlanName.ToLower().Contains(planTypeFilter));
            }

            return await query
                .OrderByDescending(p => p.IDV)
                .ThenByDescending(p => p.PolicyId)
                .Select(p => new PolicyContextDto
                {
                    PolicyId = p.PolicyId,
                    PolicyNumber = p.PolicyNumber,
                    CustomerId = p.CustomerId,
                    CustomerName = p.Customer.FullName,
                    VehicleId = p.VehicleId,
                    PlanId = p.PlanId,
                    Status = p.Status.ToString(),
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    PremiumAmount = p.PremiumAmount,
                    InvoiceAmount = p.InvoiceAmount,
                    IDV = p.IDV,
                    ClaimCount = p.ClaimCount,
                    PlanName = p.Plan.PlanName,
                    VehicleRegistrationNumber = p.Vehicle.RegistrationNumber,
                    VehicleMake = p.Vehicle.Make,
                    VehicleModel = p.Vehicle.Model,
                    VehicleYear = p.Vehicle.Year,
                    VehicleType = p.Vehicle.VehicleType,
                    FuelType = p.Vehicle.FuelType
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PolicyContextDto?> GetPolicyWithHighestPremiumAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Policies
                .AsNoTracking()
                .Where(p => p.Status != PolicyStatus.Draft)
                .OrderByDescending(p => p.PremiumAmount)
                .ThenByDescending(p => p.PolicyId)
                .Select(p => new PolicyContextDto
                {
                    PolicyId = p.PolicyId,
                    PolicyNumber = p.PolicyNumber,
                    CustomerId = p.CustomerId,
                    CustomerName = p.Customer.FullName,
                    VehicleId = p.VehicleId,
                    PlanId = p.PlanId,
                    Status = p.Status.ToString(),
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    PremiumAmount = p.PremiumAmount,
                    InvoiceAmount = p.InvoiceAmount,
                    IDV = p.IDV,
                    ClaimCount = p.ClaimCount,
                    PlanName = p.Plan.PlanName,
                    VehicleRegistrationNumber = p.Vehicle.RegistrationNumber,
                    VehicleMake = p.Vehicle.Make,
                    VehicleModel = p.Vehicle.Model,
                    VehicleYear = p.Vehicle.Year,
                    VehicleType = p.Vehicle.VehicleType,
                    FuelType = p.Vehicle.FuelType
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<int> GetSoldPoliciesCountByPolicyTypeAsync(string policyType, bool includePendingPayment = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(policyType))
            {
                return 0;
            }

            var normalizedType = policyType.Trim().Replace(" ", string.Empty).ToLowerInvariant();

            var soldStatuses = new List<PolicyStatus>
            {
                PolicyStatus.Active,
                PolicyStatus.Claimed,
                PolicyStatus.Expired,
                PolicyStatus.Cancelled
            };

            if (includePendingPayment)
            {
                soldStatuses.Add(PolicyStatus.PendingPayment);
            }

            return await _context.Policies
                .AsNoTracking()
                .Where(p => soldStatuses.Contains(p.Status))
                .Where(p => p.Plan != null && p.Plan.PolicyType != null)
                .Where(p => p.Plan.PolicyType.Replace(" ", "").ToLower() == normalizedType)
                .CountAsync(cancellationToken);
        }

        public async Task<int> GetTotalPoliciesCountAsync(string? planNameContains = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Policies
                .AsNoTracking()
                .Include(p => p.Plan)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(planNameContains))
            {
                var filter = planNameContains.Trim();
                query = query.Where(p => p.Plan.PlanName.Contains(filter));
            }

            return await query.CountAsync(cancellationToken);
        }

        public async Task<int> GetRegisteredVehiclesCountAsync(bool activePoliciesOnly = false, CancellationToken cancellationToken = default)
        {
            var query = _context.Policies
                .AsNoTracking()
                .AsQueryable();

            if (activePoliciesOnly)
            {
                query = query.Where(p => p.Status == PolicyStatus.Active);
            }

            return await query
                .Select(p => p.VehicleId)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        public async Task<decimal> GetTotalCollectedPremiumsAsync(bool activePoliciesOnly = false, CancellationToken cancellationToken = default)
        {
            var query = _context.Policies
                .AsNoTracking()
                .AsQueryable();

            if (activePoliciesOnly)
            {
                query = query.Where(p => p.Status == PolicyStatus.Active);
            }

            return await query.SumAsync(p => (decimal?)p.PremiumAmount, cancellationToken) ?? 0m;
        }
    }
}
