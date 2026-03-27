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
        Task AddAsync(VehicleApplication application);
        Task<VehicleApplication?> GetByIdAsync(int id);
        Task<List<VehicleApplication>> GetPendingByAgentIdAsync(int agentId);
        Task<List<VehicleApplication>> GetAllByAgentIdAsync(int agentId);
        Task<List<VehicleApplication>> GetAllAsync();
        Task<List<VehicleApplication>> GetByCustomerIdAsync(int customerId);
        Task UpdateAsync(VehicleApplication application);
        Task SaveChangesAsync();
    }
}
