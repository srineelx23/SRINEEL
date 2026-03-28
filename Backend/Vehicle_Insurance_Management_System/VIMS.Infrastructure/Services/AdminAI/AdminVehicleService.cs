using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Services.AdminAI
{
    public class AdminVehicleService : IAdminVehicleService
    {
        private readonly VehicleInsuranceContext _context;

        public AdminVehicleService(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<VehicleContextDto?> GetVehicleByIdAsync(int vehicleId, CancellationToken cancellationToken = default)
        {
            return await _context.Vehicles
                .AsNoTracking()
                .Where(v => v.VehicleId == vehicleId)
                .Select(Map())
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<VehicleContextDto>> GetVehiclesByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Vehicles
                .AsNoTracking()
                .Where(v => v.CustomerId == userId)
                .OrderBy(v => v.RegistrationNumber)
                .Select(Map())
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<VehicleContextDto>> GetRecentVehiclesAsync(int take = 50, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 50 : take;
            return await _context.Vehicles
                .AsNoTracking()
                .OrderByDescending(v => v.VehicleId)
                .Take(safeTake)
                .Select(Map())
                .ToListAsync(cancellationToken);
        }

        private static System.Linq.Expressions.Expression<Func<VIMS.Domain.Entities.Vehicle, VehicleContextDto>> Map()
        {
            return v => new VehicleContextDto
            {
                VehicleId = v.VehicleId,
                CustomerId = v.CustomerId,
                RegistrationNumber = v.RegistrationNumber,
                Make = v.Make,
                Model = v.Model,
                Year = v.Year,
                FuelType = v.FuelType,
                VehicleType = v.VehicleType
            };
        }
    }
}