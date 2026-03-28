using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IAdminGarageService
    {
        Task<IReadOnlyList<GarageContextDto>> GetAllGaragesAsync(CancellationToken cancellationToken = default);
    }
}