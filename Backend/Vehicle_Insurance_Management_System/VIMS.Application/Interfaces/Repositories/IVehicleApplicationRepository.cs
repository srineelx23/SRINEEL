using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IVehicleApplicationRepository
    {
        public Task AddAsync(VehicleApplication application);
        public Task<VehicleApplication?> GetByIdAsync(int id);
        Task<List<VehicleApplication>> GetPendingByAgentIdAsync(int agentId);
        public Task<List<VehicleApplication>> GetAllByAgentIdAsync(int agentId);
        Task<List<VehicleApplication>> GetByCustomerIdAsync(int customerId);
        public Task SaveChangesAsync();
    }
}
