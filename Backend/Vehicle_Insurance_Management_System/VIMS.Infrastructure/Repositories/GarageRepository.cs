using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Entities;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Repositories
{
    public class GarageRepository : IGarageRepository
    {
        private readonly VehicleInsuranceContext _context;

        public GarageRepository(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Garage>> GetAllAsync()
        {
            return await _context.Garages.ToListAsync();
        }

        public async Task<Garage?> GetByIdAsync(int id)
        {
            return await _context.Garages.FindAsync(id);
        }

        public async Task<Garage> AddAsync(Garage garage)
        {
            await _context.Garages.AddAsync(garage);
            await _context.SaveChangesAsync();
            return garage;
        }

        public async Task UpdateAsync(Garage garage)
        {
            _context.Entry(garage).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var garage = await _context.Garages.FindAsync(id);
            if (garage != null)
            {
                _context.Garages.Remove(garage);
                await _context.SaveChangesAsync();
            }
        }
    }
}
