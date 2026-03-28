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
                    VehicleYear = p.Vehicle.Year
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
                    VehicleYear = p.Vehicle.Year
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PolicyContextDto>> GetRecentPoliciesAsync(int take = 20, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 20 : take;
            return await _context.Policies
                .AsNoTracking()
                .OrderByDescending(p => p.StartDate)
                .Take(safeTake)
                .Select(p => new PolicyContextDto
                {
                    PolicyId = p.PolicyId,
                    PolicyNumber = p.PolicyNumber,
                    CustomerId = p.CustomerId,
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
                    VehicleYear = p.Vehicle.Year
                })
                .ToListAsync(cancellationToken);
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
