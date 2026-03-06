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
        private readonly IPolicyTransferRepository _policyTransferRepository;
        private readonly IAuditService _auditService;

        public AgentService(IVehicleApplicationRepository appRepo, IVehicleRepository vehicleRepo, IPolicyPlanRepository policyPlanRepository, IPolicyRepository policyRepository, IPricingService pricingService, IPolicyTransferRepository policyTransferRepository, IAuditService auditService)
        {
            _vehicleApplicationRepository = appRepo;
            _vehicleRepository = vehicleRepo;
            _policyPlanRepository = policyPlanRepository;
            _policyRepository = policyRepository;
            _pricingService = pricingService;
            _policyTransferRepository = policyTransferRepository;
            _auditService = auditService;
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
                await _auditService.LogActionAsync("PolicyApplicationRejected", "Policy", $"Agent rejected application: {app.RegistrationNumber}. Reason: {app.RejectionReason}", "VehicleApplication", app.VehicleApplicationId.ToString());

                // If this was a transfer application, mark transfer as cancelled
                if (app.IsTransfer)
                {
                    var transfers = await _policyTransferRepository.GetByNewVehicleApplicationIdAsync(applicationId);
                    foreach (var t in transfers)
                    {
                        t.Status = PolicyTransferStatus.Cancelled;
                        t.UpdatedAt = DateTime.UtcNow;
                    }
                    await _policyTransferRepository.SaveChangesAsync();
                }

                return;
            }

            // ==========================
            // APPROVAL FLOW
            // ==========================

            var plan = await _policyPlanRepository.GetPolicyPlanAsync(app.PlanId);

            if (plan == null || plan.Status != PlanStatus.Active)
                throw new BadRequestException("Invalid or inactive plan");

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
                false
            );

            // ==========================
            // TRANSFER-SPECIFIC APPROVAL
            // ==========================
            if (app.IsTransfer)
            {
                //try 
                //{
                    // Find the matching PolicyTransfer record
                    var transfers = await _policyTransferRepository.GetByNewVehicleApplicationIdAsync(applicationId);
                    var transfer = transfers.FirstOrDefault();
                    
                    if (transfer != null)
                    {
                        // Get old vehicle (linked to old VehicleApplication via the original policy)
                        var oldPolicy = transfer.Policy;
                        var oldVehicle = oldPolicy?.Vehicle;

                            if (oldVehicle != null)
                            {
                                if (oldPolicy != null)
                                {
                                    // 1. Deactivate old policy
                                    oldPolicy.Status = PolicyStatus.Cancelled;
                                    await _policyRepository.UpdateAsync(oldPolicy);
                                }

                                // 2. Update vehicle ownership
                                oldVehicle.CustomerId = app.CustomerId;
                                oldVehicle.VehicleApplicationId = app.VehicleApplicationId;
                                _vehicleRepository.Update(oldVehicle);

                                // 3. Create new policy for the recipient (Srineel)
                                var now = DateTime.UtcNow;
                                var newPolicy = new Policy
                                {
                                    PolicyNumber = $"POL-{now.Year}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                                    CustomerId = app.CustomerId,
                                    AgentId = app.AssignedAgentId,
                                    VehicleId = oldVehicle.VehicleId,
                                    PlanId = app.PlanId,
                                    SelectedYears = app.PolicyYears,
                                    
                                    // Inheritance from Vishal's progress
                                    StartDate = oldPolicy?.StartDate ?? now,
                                    EndDate = oldPolicy?.EndDate ?? now.AddYears(app.PolicyYears),
                                    CurrentYearNumber = oldPolicy?.CurrentYearNumber ?? 0,
                                    CurrentYearEndDate = oldPolicy?.CurrentYearEndDate ?? now,
                                    IsCurrentYearPaid = oldPolicy?.IsCurrentYearPaid ?? false,
                                    
                                    PremiumAmount = pricing.Premium,
                                    InvoiceAmount = dto.InvoiceAmount,
                                    IDV = pricing.IDV,
                                    InitialKilometersDriven = app.KilometersDriven,
                                    // Srineel always starts with PendingPayment to pay the transfer fee
                                    Status = PolicyStatus.PendingPayment
                                };

                                await _policyRepository.AddAsync(newPolicy);

                                // 4. Finalize Transfer
                                transfer.Status = PolicyTransferStatus.Completed;
                                transfer.UpdatedAt = DateTime.UtcNow;
                                await _policyTransferRepository.SaveChangesAsync();
                            }
                    }

                    app.Status = VehicleApplicationStatus.Approved;
                    await _vehicleApplicationRepository.SaveChangesAsync();
                    await _auditService.LogActionAsync("PolicyApplicationApproved", "Policy", $"Agent approved transfer application: {app.RegistrationNumber}", "VehicleApplication", app.VehicleApplicationId.ToString());
                    return; // EXIT EARLY IF IT WAS A TRANSFER
                //} 
                //catch (Exception ex) 
                //{
                    //System.IO.File.WriteAllText("C:\\Temp\\agent_transfer.log", "Transfer Error: " + ex.ToString());
                    //throw;
                //}
            }

            // ==========================
            // NORMAL (NON-TRANSFER) APPROVAL
            // ==========================
            var existingVehicle = await _vehicleRepository.GetByRegistrationNumberAsync(app.RegistrationNumber);
            var vehicle = existingVehicle;
            
            if (vehicle == null)
            {
                vehicle = new Vehicle
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
            }
            else 
            {
                // Update ownership if it already exists
                vehicle.CustomerId = app.CustomerId;
                vehicle.VehicleApplicationId = app.VehicleApplicationId;
                _vehicleRepository.Update(vehicle);
            }
            
            await _vehicleApplicationRepository.SaveChangesAsync();

            var nowNormal = DateTime.UtcNow;

            var policy = new Policy
            {
                PolicyNumber = $"POL-{nowNormal.Year}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                CustomerId = app.CustomerId,
                AgentId = app.AssignedAgentId,
                VehicleId = vehicle.VehicleId,
                PlanId = app.PlanId,
                SelectedYears = app.PolicyYears,
                StartDate = nowNormal,
                EndDate = nowNormal.AddYears(app.PolicyYears),
                CurrentYearNumber = 0,
                CurrentYearEndDate = nowNormal,
                IsCurrentYearPaid = false,
                PremiumAmount = pricing.Premium,
                InvoiceAmount = dto.InvoiceAmount,
                IDV = pricing.IDV,
                InitialKilometersDriven = app.KilometersDriven,
                Status = PolicyStatus.PendingPayment
            };

            await _policyRepository.AddAsync(policy);

            app.Status = VehicleApplicationStatus.Approved;
            await _vehicleApplicationRepository.SaveChangesAsync();
            await _auditService.LogActionAsync("PolicyApplicationApproved", "Policy", $"Agent approved application: {app.RegistrationNumber}", "VehicleApplication", app.VehicleApplicationId.ToString());
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
                            .Where(p => p.CustomerId == v.CustomerId)
                            .OrderByDescending(p => p.PolicyId)
                            .Take(1)
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
