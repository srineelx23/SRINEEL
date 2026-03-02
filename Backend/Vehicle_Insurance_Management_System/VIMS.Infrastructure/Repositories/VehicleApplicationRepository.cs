using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Repositories
{
    public class VehicleApplicationRepository:IVehicleApplicationRepository
    {
        private readonly VehicleInsuranceContext _context;

        public VehicleApplicationRepository(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task AddAsync(VehicleApplication application)
        {
            await _context.VehicleApplications.AddAsync(application);
        }

        public async Task<VehicleApplication?> GetByIdAsync(int id)
        {
            return await _context.VehicleApplications
                .Include(a => a.Documents)
                .FirstOrDefaultAsync(a => a.VehicleApplicationId == id);
        }
        public async Task<List<VehicleApplication>> GetPendingByAgentIdAsync(int agentId)
        {
            return await _context.VehicleApplications
                .Where(a =>
                    a.AssignedAgentId == agentId &&
                    a.Status == VehicleApplicationStatus.UnderReview)
                .Include(a => a.Documents)   // optional if agent needs document info
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Vehicle>> GetVehiclesByAgentIdAsync(int agentId)
        {
            return await _context.Vehicles
        .Include(v => v.Customer)
        .Include(v => v.VehicleApplication)
        .Where(v => v.VehicleApplication.AssignedAgentId == agentId)
        .ToListAsync();
        }
        public async Task<List<VehicleApplication>> GetByCustomerIdAsync(int customerId)
        {
            return await _context.VehicleApplications
                .Where(a => a.CustomerId == customerId)
                  // optional if you want document info
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }
        public async Task<List<VehicleApplication>> GetAllByAgentIdAsync(int agentId)
        {
            return await _context.VehicleApplications
                .Where(a => a.AssignedAgentId == agentId)
                .Include(a => a.Customer)
                .Include(a => a.Documents)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
