using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Services.AdminAI
{
    public class AdminGarageService : IAdminGarageService
    {
        private readonly VehicleInsuranceContext _context;

        public AdminGarageService(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<GarageContextDto>> GetAllGaragesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Garages
                .AsNoTracking()
                .OrderBy(g => g.GarageName)
                .Select(g => new GarageContextDto
                {
                    GarageId = g.GarageId,
                    GarageName = g.GarageName,
                    Latitude = g.Latitude,
                    Longitude = g.Longitude,
                    PhoneNumber = g.PhoneNumber,
                    CreatedAt = g.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }
    }
}