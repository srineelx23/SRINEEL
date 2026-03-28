using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IAdminVehicleService
    {
        Task<VehicleContextDto?> GetVehicleByIdAsync(int vehicleId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VehicleContextDto>> GetVehiclesByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VehicleContextDto>> GetRecentVehiclesAsync(int take = 50, CancellationToken cancellationToken = default);
    }
}