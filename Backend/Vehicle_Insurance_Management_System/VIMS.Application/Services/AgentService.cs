using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
        private readonly IFileStorageService _fileStorageService;
        private readonly INotificationService _notificationService;
        private readonly IOcrService _ocrService;

        public AgentService(IVehicleApplicationRepository appRepo, IVehicleRepository vehicleRepo, IPolicyPlanRepository policyPlanRepository, IPolicyRepository policyRepository, IPricingService pricingService, IPolicyTransferRepository policyTransferRepository, IAuditService auditService, IFileStorageService fileStorageService, INotificationService notificationService, IOcrService ocrService)
        {
            _vehicleApplicationRepository = appRepo;
            _vehicleRepository = vehicleRepo;
            _policyPlanRepository = policyPlanRepository;
            _policyRepository = policyRepository;
            _pricingService = pricingService;
            _policyTransferRepository = policyTransferRepository;
            _auditService = auditService;
            _fileStorageService = fileStorageService;
            _notificationService = notificationService;
            _ocrService = ocrService;
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

                // Delete physical documents and folders
                string storageIdentifier;
                if (app.IsTransfer)
                {
                    // Find transfer record to get vehicleId
                    var transfers = await _policyTransferRepository.GetByNewVehicleApplicationIdAsync(applicationId);
                    var transfer = transfers.FirstOrDefault();
                    if (transfer != null)
                    {
                        storageIdentifier = $"{app.CustomerId}/{transfer.Policy?.VehicleId}/transfer_policies/transfer_{transfer.PolicyTransferId}";
                    }
                    else
                    {
                        storageIdentifier = $"{app.CustomerId}/temp_app_{applicationId}";
                    }
                }
                else
                {
                    storageIdentifier = $"{app.CustomerId}/temp_app_{applicationId}";
                }

                await _fileStorageService.DeleteDirectoryAsync("user", storageIdentifier);
                app.Documents.Clear();

                await _vehicleApplicationRepository.SaveChangesAsync();
                await _auditService.LogActionAsync("PolicyApplicationRejected", "Policy", $"Agent rejected application: {app.RegistrationNumber}. Reason: {app.RejectionReason}", "VehicleApplication", app.VehicleApplicationId.ToString());
                await _notificationService.CreateNotificationAsync(app.CustomerId, "Policy Request Rejected", $"Your policy application for {app.RegistrationNumber} has been rejected by the agent. Reason: {app.RejectionReason}", NotificationType.PolicyRejected, "VehicleApplication", app.VehicleApplicationId.ToString());


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
                    await _notificationService.CreateNotificationAsync(app.CustomerId, "Policy Transfer Approved", $"Your policy transfer request for {app.RegistrationNumber} has been approved. You can now proceed to payment.", NotificationType.PolicyTransferStatusChanged, "VehicleApplication", app.VehicleApplicationId.ToString());
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

            // REORGANIZE FILES: Move from temp_app_{id} to vehicle_{id}
            if (!app.IsTransfer)
            {
                string sourceIdentifier = $"{app.CustomerId}/temp_app_{applicationId}";
                string targetIdentifier = $"{app.CustomerId}/{vehicle.VehicleId}";
                
                await _fileStorageService.MoveDirectoryAsync("user", sourceIdentifier, "user", targetIdentifier);

                // Update document paths in DB
                foreach (var doc in app.Documents)
                {
                    doc.FilePath = doc.FilePath.Replace($"temp_app_{applicationId}", vehicle.VehicleId.ToString());
                }
                await _vehicleApplicationRepository.SaveChangesAsync();
            }

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
            await _notificationService.CreateNotificationAsync(app.CustomerId, "Policy Request Approved", $"Great news! Your policy application for {app.RegistrationNumber} has been approved. Please complete the premium payment.", NotificationType.PolicyApproved, "Policy", policy.PolicyId.ToString());
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

        public async Task<AgentApplicationValidationResultDTO> ValidateApplicationDocumentsAsync(int applicationId)
        {
            var result = new AgentApplicationValidationResultDTO();

            var app = await _vehicleApplicationRepository.GetByIdAsync(applicationId);
            if (app == null)
            {
                throw new NotFoundException("Application not found");
            }

            var invoiceDoc = app.Documents?.FirstOrDefault(d => string.Equals(d.DocumentType, "Invoice", StringComparison.OrdinalIgnoreCase));
            var rcDoc = app.Documents?.FirstOrDefault(d => string.Equals(d.DocumentType, "RC", StringComparison.OrdinalIgnoreCase));

            if (invoiceDoc == null || string.IsNullOrWhiteSpace(invoiceDoc.FilePath) || rcDoc == null || string.IsNullOrWhiteSpace(rcDoc.FilePath))
            {
                result.Errors.Add("Required documents are missing (Invoice/RC).");
                result.RiskScore = 100;
                return result;
            }

            var rcText = await _ocrService.ExtractTextAsync(rcDoc.FilePath);
            var invoiceText = await _ocrService.ExtractTextAsync(invoiceDoc.FilePath);

            if (string.IsNullOrWhiteSpace(rcText))
            {
                result.Errors.Add("Could not read RC document text.");
            }

            if (string.IsNullOrWhiteSpace(invoiceText))
            {
                result.Errors.Add("Could not read Invoice document text.");
            }

            var rcEngine = ExtractEngineNumber(rcText);
            var invoiceEngine = ExtractEngineNumber(invoiceText);
            var rcChassis = ExtractChassisNumber(rcText);
            var invoiceChassis = ExtractChassisNumber(invoiceText);

            if (!string.IsNullOrWhiteSpace(rcEngine) && !string.IsNullOrWhiteSpace(invoiceEngine) && !string.Equals(NormalizeAlnum(rcEngine), NormalizeAlnum(invoiceEngine), StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Document mismatch: RC engine number '{rcEngine}' does not match Invoice engine number '{invoiceEngine}'.");
            }

            if (!string.IsNullOrWhiteSpace(rcChassis) && !string.IsNullOrWhiteSpace(invoiceChassis) && !string.Equals(NormalizeAlnum(rcChassis), NormalizeAlnum(invoiceChassis), StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Document mismatch: RC chassis number '{rcChassis}' does not match Invoice chassis number '{invoiceChassis}'.");
            }

            var rcYear = ExtractYear(rcText);
            if (rcYear.HasValue && rcYear.Value != app.Year)
            {
                result.Errors.Add($"Application mismatch: Entered year '{app.Year}' does not match RC year '{rcYear.Value}'.");
            }

            var rcFuel = ExtractFuelType(rcText);
            if (!string.IsNullOrWhiteSpace(rcFuel) && !string.Equals(NormalizeFuel(rcFuel), NormalizeFuel(app.FuelType), StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Application mismatch: Entered fuel '{app.FuelType}' does not match RC fuel '{rcFuel}'.");
            }

            var rcUsage = ExtractUsageType(rcText);
            if (!string.IsNullOrWhiteSpace(rcUsage) && !string.Equals(NormalizeUsage(rcUsage), NormalizeUsage(app.VehicleType), StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"Application mismatch: Entered usage '{app.VehicleType}' does not match RC usage '{rcUsage}'.");
            }

            var invoiceAmount = ExtractInvoiceAmount(invoiceText);
            if (invoiceAmount.HasValue && app.InvoiceAmount > 0 && invoiceAmount.Value != app.InvoiceAmount)
            {
                result.Errors.Add($"Application mismatch: Entered invoice amount '{app.InvoiceAmount}' does not match Invoice document amount '{invoiceAmount.Value}'.");
            }

            result.RiskScore = CalculateRiskScore(result.Errors.Count);
            return result;
        }

        private static int CalculateRiskScore(int mismatchCount)
        {
            if (mismatchCount <= 0)
            {
                return 0;
            }

            var score = mismatchCount * 20;
            if (mismatchCount >= 3)
            {
                score += 10;
            }

            return Math.Min(100, score);
        }

        private static string ExtractEngineNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var labeledMatch = Regex.Match(text,
                @"(?:Engine\s*(?:/\s*Motor)?\s*(?:No\.?|Number)?|Engine\s*(?:No\.?|Number)|Engine\s*/\s*Motor\s*Number)\s*(?:\([^\)]*\))?\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-]{5,})",
                RegexOptions.IgnoreCase);
            if (labeledMatch.Success)
            {
                return labeledMatch.Groups[1].Value.Trim();
            }

            var lineMatch = Regex.Match(text,
                @"(?im)^\s*Engine\s*(?:/\s*Motor)?\s*(?:No\.?|Number)?\s*(?:\([^\)]*\))?\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-]{5,})\s*$");
            return lineMatch.Success ? lineMatch.Groups[1].Value.Trim() : string.Empty;
        }

        private static string ExtractChassisNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var labeledMatch = Regex.Match(text,
                @"(?:Chassis\s*(?:No\.?|Number)?|CH\.?\s*NO\.?)\s*(?:\([^\)]*\))?\s*[:\-]?\s*([A-Z0-9\-]{8,})",
                RegexOptions.IgnoreCase);
            if (labeledMatch.Success)
            {
                return labeledMatch.Groups[1].Value.Trim();
            }

            return string.Empty;
        }

        private static int? ExtractYear(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = Regex.Match(text, @"Year\s*of\s*Manufacture\s*[:\s]*(-?\d{4})", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var year))
            {
                return year;
            }

            return null;
        }

        private static string ExtractFuelType(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var match = Regex.Match(text, @"Fuel\s*Type\s*[:\s]*([^\r\n]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static string ExtractUsageType(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (text.Contains("Commercial", StringComparison.OrdinalIgnoreCase))
            {
                return "Commercial";
            }

            return "Private";
        }

        private static decimal? ExtractInvoiceAmount(string invoiceText)
        {
            if (string.IsNullOrWhiteSpace(invoiceText))
            {
                return null;
            }

            var exShowroomMatch = Regex.Match(invoiceText,
                @"Ex[\s\-]*Showroom\s*Price[^\r\n]*[\r\n]+([^\r\n]+)",
                RegexOptions.IgnoreCase);

            if (exShowroomMatch.Success)
            {
                var nextLine = exShowroomMatch.Groups[1].Value;
                var priceMatches = Regex.Matches(nextLine, @"-?(?:\d{1,2},)?\d{2},\d{3}|-?\d{5,}");
                if (priceMatches.Count > 0)
                {
                    var lastNum = priceMatches[priceMatches.Count - 1].Value.Replace(",", "");
                    if (decimal.TryParse(lastNum, out var amount))
                    {
                        return amount;
                    }
                }
            }

            return null;
        }

        private static string NormalizeAlnum(string value)
        {
            return (value ?? string.Empty).ToUpperInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
        }

        private static string NormalizeFuel(string value)
        {
            var v = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (v.Contains("petrol")) return "petrol";
            if (v.Contains("diesel")) return "diesel";
            if (v.Contains("hybrid")) return "hybrid";
            if (v.Contains("electric") || v == "ev") return "ev";
            if (v.Contains("cng")) return "cng";
            return v;
        }

        private static string NormalizeUsage(string value)
        {
            var v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return v.Contains("commercial") ? "commercial" : "private";
        }
    }
}
