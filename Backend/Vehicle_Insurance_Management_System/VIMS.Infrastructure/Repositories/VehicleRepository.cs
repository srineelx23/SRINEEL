using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Entities;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Repositories
{
    public class VehicleRepository:IVehicleRepository
    {
        private readonly VehicleInsuranceContext _context;

        public VehicleRepository(VehicleInsuranceContext context)
        {
            _context = context;
        }
        public async Task AddAsync(Vehicle vehicle)
        {
            await _context.Vehicles.AddAsync(vehicle);
            await _context.SaveChangesAsync();
        }
        public async Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber)
        {
            return await _context.Vehicles
                .FirstOrDefaultAsync(v => v.RegistrationNumber == registrationNumber);
        }
        public async Task<List<Vehicle>> GetVehiclesByAgentIdAsync(int agentId)
        {
            return await _context.Vehicles
       .Include(v => v.Customer)
       .Include(v => v.Policies)
       .Include(v => v.VehicleApplication)
           .ThenInclude(a => a.Documents)
       .Where(v => v.VehicleApplication.AssignedAgentId == agentId)
       .ToListAsync();
        }
    }
}
