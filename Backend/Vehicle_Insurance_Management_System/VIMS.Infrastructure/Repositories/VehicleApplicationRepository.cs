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
            var app = await _context.VehicleApplications
                .Include(a => a.Customer)
                .Include(a => a.AssignedAgent)
                .Include(a => a.Documents)
                .FirstOrDefaultAsync(a => a.VehicleApplicationId == id);

            return app;
        }

        public async Task<List<VehicleApplication>> GetPendingByAgentIdAsync(int agentId)
        {
            var apps = await _context.VehicleApplications
                .Where(a =>
                    a.AssignedAgentId == agentId &&
                    a.Status == VehicleApplicationStatus.UnderReview)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new VehicleApplication
                {
                    VehicleApplicationId = a.VehicleApplicationId,
                    AssignedAgentId = a.AssignedAgentId,
                    CreatedAt = a.CreatedAt,
                    CustomerId = a.CustomerId,
                    FuelType = a.FuelType,
                    InvoiceAmount = a.InvoiceAmount,
                    KilometersDriven = a.KilometersDriven,
                    Make = a.Make,
                    Model = a.Model,
                    PlanId = a.PlanId,
                    PolicyYears = a.PolicyYears,
                    RegistrationNumber = a.RegistrationNumber,
                    RejectionReason = a.RejectionReason,
                    Status = a.Status,
                    VehicleType = a.VehicleType,
                    Year = a.Year,
                    IsTransfer = a.IsTransfer,
                    Customer = new User { FullName = a.Customer.FullName, Email = a.Customer.Email }
                })
                .ToListAsync();

            // load documents for each app
            var appIds = apps.Select(a => a.VehicleApplicationId).ToList();
            var docs = await _context.VehicleDocuments.Where(d => appIds.Contains(d.VehicleApplicationId)).ToListAsync();
            foreach (var a in apps)
                a.Documents = docs.Where(d => d.VehicleApplicationId == a.VehicleApplicationId).ToList();

            return apps;
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
            var apps = await _context.VehicleApplications
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new VehicleApplication
                {
                    VehicleApplicationId = a.VehicleApplicationId,
                    AssignedAgentId = a.AssignedAgentId,
                    CreatedAt = a.CreatedAt,
                    CustomerId = a.CustomerId,
                    FuelType = a.FuelType,
                    InvoiceAmount = a.InvoiceAmount,
                    KilometersDriven = a.KilometersDriven,
                    Make = a.Make,
                    Model = a.Model,
                    PlanId = a.PlanId,
                    PolicyYears = a.PolicyYears,
                    RegistrationNumber = a.RegistrationNumber,
                    RejectionReason = a.RejectionReason,
                    Status = a.Status,
                    VehicleType = a.VehicleType,
                    Year = a.Year
                })
                .ToListAsync();

            var appIds = apps.Select(a => a.VehicleApplicationId).ToList();
            var docs = await _context.VehicleDocuments.Where(d => appIds.Contains(d.VehicleApplicationId)).ToListAsync();
            foreach (var a in apps)
                a.Documents = docs.Where(d => d.VehicleApplicationId == a.VehicleApplicationId).ToList();

            return apps;
        }
        public async Task<List<VehicleApplication>> GetAllByAgentIdAsync(int agentId)
        {
            var apps = await _context.VehicleApplications
                .Where(a => a.AssignedAgentId == agentId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new VehicleApplication
                {
                    VehicleApplicationId = a.VehicleApplicationId,
                    AssignedAgentId = a.AssignedAgentId,
                    CreatedAt = a.CreatedAt,
                    CustomerId = a.CustomerId,
                    FuelType = a.FuelType,
                    InvoiceAmount = a.InvoiceAmount,
                    KilometersDriven = a.KilometersDriven,
                    Make = a.Make,
                    Model = a.Model,
                    PlanId = a.PlanId,
                    PolicyYears = a.PolicyYears,
                    RegistrationNumber = a.RegistrationNumber,
                    RejectionReason = a.RejectionReason,
                    Status = a.Status,
                    VehicleType = a.VehicleType,
                    Year = a.Year
                })
                .ToListAsync();

            var appIds = apps.Select(a => a.VehicleApplicationId).ToList();
            var docs = await _context.VehicleDocuments.Where(d => appIds.Contains(d.VehicleApplicationId)).ToListAsync();
            foreach (var a in apps)
                a.Documents = docs.Where(d => d.VehicleApplicationId == a.VehicleApplicationId).ToList();

            // load customers separately and attach minimal customer info if needed
            var customerIds = apps.Select(a => a.CustomerId).Distinct().ToList();
            var customers = await _context.Users.Where(u => customerIds.Contains(u.UserId)).ToListAsync();
            foreach (var a in apps)
            {
                a.Customer = customers.FirstOrDefault(c => c.UserId == a.CustomerId);
            }

            return apps;
        }

        public async Task<List<VehicleApplication>> GetAllAsync()
        {
            var apps = await _context.VehicleApplications
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new VehicleApplication
                {
                    VehicleApplicationId = a.VehicleApplicationId,
                    AssignedAgentId = a.AssignedAgentId,
                    CreatedAt = a.CreatedAt,
                    CustomerId = a.CustomerId,
                    FuelType = a.FuelType,
                    InvoiceAmount = a.InvoiceAmount,
                    KilometersDriven = a.KilometersDriven,
                    Make = a.Make,
                    Model = a.Model,
                    PlanId = a.PlanId,
                    PolicyYears = a.PolicyYears,
                    RegistrationNumber = a.RegistrationNumber,
                    RejectionReason = a.RejectionReason,
                    Status = a.Status,
                    VehicleType = a.VehicleType,
                    Year = a.Year,
                    IsTransfer = a.IsTransfer
                })
                .ToListAsync();

            var appIds = apps.Select(a => a.VehicleApplicationId).ToList();
            var docs = await _context.VehicleDocuments.Where(d => appIds.Contains(d.VehicleApplicationId)).ToListAsync();
            foreach (var a in apps)
            {
                a.Documents = docs.Where(d => d.VehicleApplicationId == a.VehicleApplicationId).ToList();
            }

            var customerIds = apps.Select(a => a.CustomerId).Distinct().ToList();
            var customers = await _context.Users.Where(u => customerIds.Contains(u.UserId)).ToListAsync();
            foreach (var a in apps)
            {
                a.Customer = customers.FirstOrDefault(c => c.UserId == a.CustomerId);
            }

            return apps;
        }
        public async Task UpdateAsync(VehicleApplication application)
        {
            _context.VehicleApplications.Update(application);
            await _context.SaveChangesAsync();
        }
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
