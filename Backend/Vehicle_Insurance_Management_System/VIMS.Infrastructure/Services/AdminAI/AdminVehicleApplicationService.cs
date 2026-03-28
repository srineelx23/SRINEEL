using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Services.AdminAI
{
    public class AdminVehicleApplicationService : IAdminVehicleApplicationService
    {
        private readonly VehicleInsuranceContext _context;

        public AdminVehicleApplicationService(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<VehicleApplicationContextDto?> GetApplicationByIdAsync(int applicationId, CancellationToken cancellationToken = default)
        {
            return await _context.VehicleApplications
                .AsNoTracking()
                .Where(a => a.VehicleApplicationId == applicationId)
                .Select(Map())
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<VehicleApplicationContextDto>> GetApplicationsByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.VehicleApplications
                .AsNoTracking()
                .Where(a => a.CustomerId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(Map())
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<VehicleApplicationContextDto>> GetRecentApplicationsAsync(int take = 50, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 50 : take;
            return await _context.VehicleApplications
                .AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .Take(safeTake)
                .Select(Map())
                .ToListAsync(cancellationToken);
        }

        private static System.Linq.Expressions.Expression<Func<VIMS.Domain.Entities.VehicleApplication, VehicleApplicationContextDto>> Map()
        {
            return a => new VehicleApplicationContextDto
            {
                VehicleApplicationId = a.VehicleApplicationId,
                CustomerId = a.CustomerId,
                CustomerName = a.Customer.FullName,
                AssignedAgentId = a.AssignedAgentId,
                AssignedAgentName = a.AssignedAgent != null ? a.AssignedAgent.FullName : null,
                PlanId = a.PlanId,
                PlanName = a.Plan != null ? a.Plan.PlanName : string.Empty,
                RegistrationNumber = a.RegistrationNumber,
                Make = a.Make,
                Model = a.Model,
                Year = a.Year,
                FuelType = a.FuelType,
                VehicleType = a.VehicleType,
                KilometersDriven = a.KilometersDriven,
                PolicyYears = a.PolicyYears,
                InvoiceAmount = a.InvoiceAmount,
                Status = a.Status.ToString(),
                RejectionReason = a.RejectionReason,
                IsTransfer = a.IsTransfer,
                CreatedAt = a.CreatedAt
            };
        }
    }
}