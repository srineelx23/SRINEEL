using System.Collections.Generic;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IGarageRepository
    {
        Task<IEnumerable<Garage>> GetAllAsync();
        Task<Garage?> GetByIdAsync(int id);
        Task<Garage> AddAsync(Garage garage);
        Task UpdateAsync(Garage garage);
        Task DeleteAsync(int id);
    }
}
