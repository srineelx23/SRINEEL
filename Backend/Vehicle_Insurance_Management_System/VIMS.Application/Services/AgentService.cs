using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Application.Exceptions;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.Application.Services
{
    public class AgentService: IAgentService
    {
        private readonly IVehicleApplicationRepository _vehicleApplicationRepository;
        private readonly IVehicleRepository _vehicleRepository;
        private readonly IPolicyPlanRepository _policyPlanRepository;
        private readonly IPolicyRepository _policyRepository;
        private readonly IPricingService _pricingService;
        public AgentService(IVehicleApplicationRepository appRepo, IVehicleRepository vehicleRepo,IPolicyPlanRepository policyPlanRepository,IPolicyRepository policyRepository,IPricingService pricingService)
        {
            _vehicleApplicationRepository = appRepo;
            _vehicleRepository = vehicleRepo;
            _policyPlanRepository = policyPlanRepository;
            _policyRepository = policyRepository;
            _pricingService = pricingService;
        }
        public async Task<List<VehicleApplication>> GetMyPendingApplicationsAsync(int agentId)
        {
            return await _vehicleApplicationRepository.GetPendingByAgentIdAsync(agentId);
        }
        public async Task ReviewApplicationAsync(int applicationId, ReviewVehicleApplicationDTO dto)
        {
            var app = await _vehicleApplicationRepository.GetByIdAsync(applicationId);

            if (app == null)
                throw new NotFoundException("Application not found");

            if (!dto.Approved)
            {
                app.Status = VehicleApplicationStatus.Rejected;
                app.RejectionReason = dto.RejectionReason;

                if (app.Documents != null && app.Documents.Any())
                {
                    foreach (var doc in app.Documents)
                    {
                        var fullPath = Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "wwwroot",
                            doc.FilePath);

                        if (File.Exists(fullPath))
                            File.Delete(fullPath);
                    }

                    app.Documents.Clear();
                }

                await _vehicleApplicationRepository.SaveChangesAsync();
                return;
            }

            // ==========================
            // APPROVAL FLOW
            // ==========================

            var plan = await _policyPlanRepository.GetPolicyPlanAsync(app.PlanId);

            if (plan == null || plan.Status != PlanStatus.Active)
                throw new BadRequestException("Invalid or inactive plan");

            // 🔥 Use DTO for pricing
            var pricingDto = new CalculateQuoteDTO
            {
                InvoiceAmount = dto.InvoiceAmount,
                ManufactureYear = app.Year,
                FuelType = app.FuelType,
                VehicleType = app.VehicleType,
                KilometersDriven = app.KilometersDriven,
                PolicyYears = app.PolicyYears,
                PlanId = app.PlanId
            };

            var pricing = _pricingService.CalculateAnnualPremium(
                pricingDto,
                plan,
                false // not renewal
            );

            // ==========================
            // CREATE VEHICLE
            // ==========================
            var vehicle = new Vehicle
            {
                CustomerId = app.CustomerId,
                VehicleApplicationId = app.VehicleApplicationId,
                RegistrationNumber = app.RegistrationNumber,
                Make = app.Make,
                Model = app.Model,
                Year = app.Year,
                FuelType = app.FuelType,
                VehicleType = app.VehicleType
            };

            await _vehicleRepository.AddAsync(vehicle);
            await _vehicleApplicationRepository.SaveChangesAsync();

            // ==========================
            // CREATE POLICY
            // ==========================
            var now = DateTime.UtcNow;

            var policy = new Policy
            {
                PolicyNumber = $"POL-{now.Year}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                CustomerId = app.CustomerId,
                AgentId = app.AssignedAgentId,
                VehicleId = vehicle.VehicleId,
                PlanId = app.PlanId,

                // Contract Duration
                SelectedYears = app.PolicyYears,
                StartDate = now,
                EndDate = now.AddYears(app.PolicyYears),

                // Year Tracking
                CurrentYearNumber = 0,
                CurrentYearEndDate = now,
                IsCurrentYearPaid = false,

                // Financial
                PremiumAmount = pricing.Premium,   // annual premium only
                InvoiceAmount = dto.InvoiceAmount,
                IDV = pricing.IDV,
                InitialKilometersDriven = app.KilometersDriven,

                // Status
                Status = PolicyStatus.PendingPayment
            };

            await _policyRepository.AddAsync(policy);

            app.Status = VehicleApplicationStatus.Approved;

            await _vehicleApplicationRepository.SaveChangesAsync();
        }
        public async Task<List<AgentCustomerDetailsDTO>> GetMyApprovedCustomersAsync(int agentId)
        {
            var vehicles = await _vehicleRepository
       .GetVehiclesByAgentIdAsync(agentId);

            var result = vehicles
                .GroupBy(v => v.Customer)
                .Select(group => new AgentCustomerDetailsDTO
                {
                    CustomerId = group.Key.UserId,
                    CustomerName = group.Key.FullName,
                    Email = group.Key.Email,

                    Vehicles = group.Select(v => new VehicleDetailsDTO
                    {
                        VehicleId = v.VehicleId,
                        RegistrationNumber = v.RegistrationNumber,
                        Make = v.Make,
                        Model = v.Model,
                        Year = v.Year,

                        Documents = v.VehicleApplication.Documents
                            .Select(d => d.FilePath)
                            .ToList(),

                        Policies = v.Policies?
                            .Select(p => new PolicyDetailsDTO
                            {
                                PolicyId = p.PolicyId,
                                PolicyNumber = p.PolicyNumber,
                                Status = p.Status.ToString(),
                                PremiumAmount = p.PremiumAmount
                            }).ToList() ?? new List<PolicyDetailsDTO>()
                    }).ToList()
                })
                .ToList();

            return result;  
        }
        public async Task<List<VehicleApplication>> GetMyApplicationsAsync(int agentId)
        {
            return await _vehicleApplicationRepository
                .GetAllByAgentIdAsync(agentId);
        }
    }
}
