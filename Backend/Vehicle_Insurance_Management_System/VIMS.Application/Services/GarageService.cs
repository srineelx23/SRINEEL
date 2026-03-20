using System.Collections.Generic;
using System.Threading.Tasks;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;

namespace VIMS.Application.Services
{
    public class GarageService : IGarageService
    {
        private readonly IGarageRepository _garageRepository;

        public GarageService(IGarageRepository garageRepository)
        {
            _garageRepository = garageRepository;
        }

        public async Task<IEnumerable<Garage>> GetAllGaragesAsync()
        {
            return await _garageRepository.GetAllAsync();
        }

        public async Task<Garage?> GetGarageByIdAsync(int id)
        {
            return await _garageRepository.GetByIdAsync(id);
        }

        public async Task<Garage> CreateGarageAsync(Garage garage)
        {
            return await _garageRepository.AddAsync(garage);
        }

        public async Task UpdateGarageAsync(Garage garage)
        {
            await _garageRepository.UpdateAsync(garage);
        }

        public async Task DeleteGarageAsync(int id)
        {
            await _garageRepository.DeleteAsync(id);
        }
    }
}
