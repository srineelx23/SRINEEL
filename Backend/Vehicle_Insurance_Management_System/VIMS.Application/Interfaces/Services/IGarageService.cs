using System.Collections.Generic;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Services
{
    public interface IGarageService
    {
        Task<IEnumerable<Garage>> GetAllGaragesAsync();
        Task<Garage?> GetGarageByIdAsync(int id);
        Task<Garage> CreateGarageAsync(Garage garage);
        Task UpdateGarageAsync(Garage garage);
        Task DeleteGarageAsync(int id);
    }
}
