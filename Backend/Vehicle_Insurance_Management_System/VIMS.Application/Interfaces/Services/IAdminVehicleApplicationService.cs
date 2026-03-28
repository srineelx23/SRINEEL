using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IAdminVehicleApplicationService
    {
        Task<VehicleApplicationContextDto?> GetApplicationByIdAsync(int applicationId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VehicleApplicationContextDto>> GetApplicationsByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VehicleApplicationContextDto>> GetRecentApplicationsAsync(int take = 50, CancellationToken cancellationToken = default);
    }
}