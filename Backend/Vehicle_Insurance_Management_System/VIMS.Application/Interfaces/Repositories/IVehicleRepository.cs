using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IVehicleRepository
    {
        public Task AddAsync(Vehicle vehicle);
        Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber);
        public Task<List<Vehicle>> GetVehiclesByAgentIdAsync(int agentId);
        void Update(Vehicle vehicle);
    }
}
