using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VIMS.Application.DTOs;
using VIMS.Application.Exceptions;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.Application.Services
{
    public class CustomerService :ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IVehicleApplicationRepository _vehicleApplicationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IVehicleRepository _vehicleRepository;
        private readonly IPolicyRepository _policyRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPricingService _pricingService;
        private readonly IPolicyPlanService _policyPlanService;
        private readonly IPolicyTransferRepository _policyTransferRepository;
        private readonly IAuditService _auditService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IClaimsRepository _claimsRepository;
        private readonly INotificationService _notificationService;
        private readonly IOcrService _ocrService;
 
        public CustomerService(ICustomerRepository customerRepository, IVehicleApplicationRepository vehicleApplicationRepository, IUserRepository userRepository, IVehicleRepository vehicleRepository, IPolicyRepository policyRepository, IPaymentRepository paymentRepository, IPricingService pricingService, IPolicyPlanService policyPlanService, IPolicyTransferRepository policyTransferRepository, IAuditService auditService, IFileStorageService fileStorageService, IClaimsRepository claimsRepository, INotificationService notificationService, IOcrService ocrService)
        {
            _customerRepository = customerRepository;
            _vehicleApplicationRepository = vehicleApplicationRepository;
            _userRepository = userRepository;
            _vehicleRepository = vehicleRepository;
            _policyRepository = policyRepository;
            _paymentRepository = paymentRepository;
            _pricingService = pricingService;
            _policyPlanService = policyPlanService;
            _policyTransferRepository = policyTransferRepository;
            _auditService = auditService;
            _fileStorageService = fileStorageService;
            _claimsRepository = claimsRepository;
            _notificationService = notificationService;
            _ocrService = ocrService;
        }

        public async Task<IEnumerable<object>> ViewAllPoliciesAsync()
        {
            var plans = await _customerRepository.ViewAllPolicyPlansAsync();
            var counts = await _policyRepository.GetPlanPurchaseCountsAsync();

            return plans.Select(p => new
            {
                p.PlanId,
                p.PlanName,
                p.PolicyType,
                p.BasePremium,
                p.PolicyDurationMonths,
                p.DeductibleAmount,
                p.MaxCoverageAmount,
                p.CoversThirdParty,
                p.CoversOwnDamage,
                p.CoversTheft,
                p.ZeroDepreciationAvailable,
                p.EngineProtectionAvailable,
                p.RoadsideAssistanceAvailable,
                p.ApplicableVehicleType,
                p.Status,
                PurchaseCount = counts.ContainsKey(p.PlanId) ? counts[p.PlanId] : 0
            });
        }
        public async Task CreateApplicationAsync(CreateVehicleApplicationDTO dto,int userId)
        {
            var nowYear = DateTime.UtcNow.Year;
            var validationErrors = new List<string>();

            dto.RegistrationNumber = (dto.RegistrationNumber ?? string.Empty).Trim();
            dto.Make = (dto.Make ?? string.Empty).Trim();
            dto.Model = (dto.Model ?? string.Empty).Trim();
            dto.FuelType = (dto.FuelType ?? string.Empty).Trim();
            dto.VehicleType = (dto.VehicleType ?? string.Empty).Trim();

            if (dto.PlanId <= 0) validationErrors.Add("Plan is required.");

            if (string.IsNullOrWhiteSpace(dto.RegistrationNumber))
                validationErrors.Add("Registration number is required.");
            else
            {
                var regSanitized = dto.RegistrationNumber.ToUpperInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
                var regRegex = new Regex(@"^[A-Z]{2}[A-Z0-9]{1,3}[A-Z]{1,3}\d{1,4}$");
                if (!regRegex.IsMatch(regSanitized))
                    validationErrors.Add("Invalid vehicle registration number format.");
            }

            if (string.IsNullOrWhiteSpace(dto.Make) || dto.Make.Length < 2 || dto.Make.Length > 60)
                validationErrors.Add("Vehicle make must be between 2 and 60 characters.");

            if (string.IsNullOrWhiteSpace(dto.Model) || dto.Model.Length > 80)
                validationErrors.Add("Vehicle model is required and must not exceed 80 characters.");

            if (dto.Year < 1980 || dto.Year > nowYear)
                validationErrors.Add($"Manufacture year must be between 1980 and {nowYear}.");

            if (dto.Year > 0 && nowYear - dto.Year > 15)
                validationErrors.Add("Cannot buy insurance for vehicles aged greater than 15 years.");

            if (dto.InvoiceAmount <= 0)
                validationErrors.Add("Invoice amount must be greater than 0.");

            if (dto.KilometersDriven < 0 || dto.KilometersDriven > 999999)
                validationErrors.Add("Kilometers driven must be between 0 and 999999.");

            if (dto.PolicyYears < 1 || dto.PolicyYears > 5)
                validationErrors.Add("Policy duration must be between 1 and 5 years.");

            var normalizedFuel = NormalizeFuelType(dto.FuelType);
            if (string.IsNullOrWhiteSpace(normalizedFuel)
                || !(normalizedFuel == "petrol" || normalizedFuel == "diesel" || normalizedFuel == "hybrid" || normalizedFuel == "ev" || normalizedFuel == "cng"))
            {
                validationErrors.Add("Fuel type must be one of: Petrol, Diesel, Hybrid, EV, CNG.");
            }

            var normalizedUsage = (dto.VehicleType ?? string.Empty).Trim().ToLowerInvariant();
            if (!(normalizedUsage == "private" || normalizedUsage == "commercial"))
            {
                validationErrors.Add("Vehicle usage type must be either Private or Commercial.");
            }

            if (dto.InvoiceDocument == null || dto.RcDocument == null)
            {
                validationErrors.Add("Both Invoice and RC documents are required.");
            }
            else
            {
                ValidatePdfFile(dto.InvoiceDocument, "Invoice document", validationErrors);
                ValidatePdfFile(dto.RcDocument, "RC document", validationErrors);
            }

            if (validationErrors.Count > 0)
                throw new BadRequestException(string.Join(" | ", validationErrors));

            var plan = await _policyPlanService.GetPolicyPlanAsync(dto.PlanId);
            if (plan == null)
                throw new BadRequestException("Selected insurance plan no longer exists.");

            var postPlanErrors = new List<string>();

            if (plan.Status != PlanStatus.Active)
                postPlanErrors.Add("Selected insurance plan is not active.");

            var planVehicleGroup = NormalizeVehicleTypeGroup(plan.ApplicableVehicleType);
            if (string.IsNullOrWhiteSpace(planVehicleGroup))
                postPlanErrors.Add("Selected insurance plan has an invalid vehicle type configuration.");

            if (normalizedFuel == "ev" && !plan.ApplicableVehicleType.Contains("EV", StringComparison.OrdinalIgnoreCase))
                postPlanErrors.Add("EV fuel type is only allowed for EV plans.");

            if (normalizedFuel != "ev" && plan.ApplicableVehicleType.Contains("EV", StringComparison.OrdinalIgnoreCase))
                postPlanErrors.Add("Selected EV plan requires EV fuel type.");

            if (postPlanErrors.Count > 0)
                throw new BadRequestException(string.Join(" | ", postPlanErrors));

            if (dto.RcDocument != null && dto.InvoiceDocument != null)
            {
                var extracted = await _ocrService.ExtractVehicleDetailsAsync(dto.RcDocument, dto.InvoiceDocument);
                var documentVehicleGroup = NormalizeVehicleTypeGroup(extracted.VehicleClass);

                var documentValidationErrors = new List<string>();

                if (!string.IsNullOrEmpty(planVehicleGroup)
                    && !string.IsNullOrEmpty(documentVehicleGroup)
                    && !string.Equals(planVehicleGroup, documentVehicleGroup, StringComparison.OrdinalIgnoreCase))
                {
                    documentValidationErrors.Add($"Vehicle type does not match. Uploaded vehicle type: {FormatVehicleType(extracted.VehicleClass)}. Selected vehicle type: {FormatVehicleType(plan.ApplicableVehicleType)}.");
                }

                var selectedFuelType = NormalizeFuelType(dto.FuelType);
                var uploadedFuelType = NormalizeFuelType(extracted.FuelType);

                if (!string.IsNullOrEmpty(selectedFuelType)
                    && !string.IsNullOrEmpty(uploadedFuelType)
                    && !string.Equals(selectedFuelType, uploadedFuelType, StringComparison.OrdinalIgnoreCase))
                {
                    documentValidationErrors.Add("Fuel type selected does not match the uploaded documents.");
                }

                if (documentValidationErrors.Count > 0)
                {
                    throw new BadRequestException(string.Join(" | ", documentValidationErrors));
                }
            }

            var regNumber = dto.RegistrationNumber.ToUpper().Replace(" ", "").Replace("-", "");

            var existingVehicle = await _vehicleRepository.GetByRegistrationNumberAsync(regNumber);

            if (existingVehicle != null)
                throw new BadRequestException("This vehicle is already registered under an active policy.");
            var agent = await _userRepository.GetLeastLoadedAgentAsync();
            var userExists = await _userRepository.GetByIdAsync(userId);
            //Console.WriteLine("Incoming CustomerId: " + dto.CustomerId);
            if (userExists == null)
            {
                throw new NotFoundException("Customer does not exist in database");
            }
            var application = new VehicleApplication
            {
                CustomerId = userId,
                AssignedAgentId = agent?.UserId,
                PlanId = dto.PlanId,
                RegistrationNumber = regNumber,
                Make = dto.Make,
                Model = dto.Model,
                Year = dto.Year,
                FuelType = dto.FuelType,
                VehicleType = dto.VehicleType,
                KilometersDriven = dto.KilometersDriven,
                PolicyYears = dto.PolicyYears,
                InvoiceAmount = dto.InvoiceAmount,
                Status = VehicleApplicationStatus.UnderReview
            };

            await _vehicleApplicationRepository.AddAsync(application);
            await _vehicleApplicationRepository.SaveChangesAsync();

            // Store files in a temporary folder: user_{userId}/temp_app_{appId}
            // This will be moved to vehicle_{id} upon approval.
            string storageIdentifier = $"{userId}/temp_app_{application.VehicleApplicationId}";

            if (dto.InvoiceDocument != null)
            {
                EnsurePdfOnly(dto.InvoiceDocument, "Invoice document");
                string invoicePath = await _fileStorageService.SaveFileAsync(dto.InvoiceDocument, "user", storageIdentifier, "invoice");
                if (!string.IsNullOrEmpty(invoicePath))
                {
                    application.Documents.Add(new VehicleDocument
                    {
                        DocumentType = "Invoice",
                        FilePath = invoicePath
                    });
                }
            }

            if (dto.RcDocument != null)
            {
                EnsurePdfOnly(dto.RcDocument, "RC document");
                string rcPath = await _fileStorageService.SaveFileAsync(dto.RcDocument, "user", storageIdentifier, "rc");
                if (!string.IsNullOrEmpty(rcPath))
                {
                    application.Documents.Add(new VehicleDocument
                    {
                        DocumentType = "RC",
                        FilePath = rcPath
                    });
                }
            }

            // Update again to save document paths
            await _vehicleApplicationRepository.UpdateAsync(application);

            await _auditService.LogActionAsync("PolicyApplicationCreated", "Policy", $"Customer created a policy application for {application.Make} {application.Model}", "VehicleApplication", application.VehicleApplicationId.ToString());
            
            // Notify Customer
            await _notificationService.CreateNotificationAsync(userId, "Policy Request Submitted", $"Your policy request for {application.Make} {application.Model} ({application.RegistrationNumber}) has been submitted successfully.", NotificationType.PolicyRequestSubmitted, "VehicleApplication", application.VehicleApplicationId.ToString());
            
            // Notify Agent
            if (application.AssignedAgentId.HasValue)
            {
                await _notificationService.CreateNotificationAsync(application.AssignedAgentId.Value, "New Policy Request Assigned", $"A new policy request for {application.RegistrationNumber} has been assigned to you.", NotificationType.NewPolicyRequestAssigned, "VehicleApplication", application.VehicleApplicationId.ToString());
            }
        }

        private static void ValidatePdfFile(IFormFile file, string label, List<string> errors)
        {
            if (file == null)
            {
                errors.Add($"{label} is required.");
                return;
            }

            var extension = Path.GetExtension(file.FileName);
            var isPdfExtension = string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
            var isPdfContentType = string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
                || string.Equals(file.ContentType, "application/x-pdf", StringComparison.OrdinalIgnoreCase);

            if (!isPdfExtension || !isPdfContentType)
            {
                errors.Add($"{label} must be a PDF file only.");
            }

            const long maxSizeInBytes = 10 * 1024 * 1024;
            if (file.Length <= 0)
            {
                errors.Add($"{label} is empty.");
            }
            else if (file.Length > maxSizeInBytes)
            {
                errors.Add($"{label} exceeds maximum size of 10 MB.");
            }
        }

        private static void EnsurePdfOnly(IFormFile file, string label)
        {
            var extension = Path.GetExtension(file.FileName);
            var isPdfExtension = string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
            var isPdfContentType = string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
                || string.Equals(file.ContentType, "application/x-pdf", StringComparison.OrdinalIgnoreCase);

            if (!isPdfExtension || !isPdfContentType)
            {
                throw new BadRequestException($"{label} must be a PDF file only.");
            }
        }

        private static string NormalizeVehicleTypeGroup(string? vehicleType)
        {
            var normalized = (vehicleType ?? string.Empty).ToLowerInvariant().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);

            if (normalized.Contains("twowheeler") || normalized.Contains("2wheeler") || normalized.Contains("motorcycle") || normalized.Contains("scooter"))
            {
                return "twowheeler";
            }

            if (normalized.Contains("threewheeler") || normalized.Contains("3wheeler") || normalized.Contains("autorickshaw") || normalized.Contains("erickshaw") || normalized.Contains("rickshaw"))
            {
                return "threewheeler";
            }

            if (normalized.Contains("heavyvehicle") || normalized.Contains("hmv") || normalized.Contains("truck") || normalized.Contains("bus"))
            {
                return "heavyvehicle";
            }

            if (normalized.Contains("car") || normalized.Contains("lmv"))
            {
                return "car";
            }

            return string.Empty;
        }

        private static string NormalizeFuelType(string? fuelType)
        {
            var normalized = (fuelType ?? string.Empty).Trim().ToLowerInvariant();

            if (normalized.Contains("petrol")) return "petrol";
            if (normalized.Contains("diesel")) return "diesel";
            if (normalized.Contains("hybrid")) return "hybrid";
            if (normalized.Contains("electric") || normalized == "ev") return "ev";
            if (normalized.Contains("cng")) return "cng";

            return normalized;
        }

        private static string FormatVehicleType(string? vehicleType)
        {
            var value = (vehicleType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unknown";
            }

            return value;
        }


        public async Task<List<CustomerApplicationDTO>> GetMyApplicationsAsync(int customerId)
        {
            var apps = await _vehicleApplicationRepository.GetByCustomerIdAsync(customerId);

            return apps.Select(a => new CustomerApplicationDTO
            {
                VehicleApplicationId = a.VehicleApplicationId,
                RegistrationNumber = a.RegistrationNumber,
                Make = a.Make,
                Model = a.Model,
                InvoiceAmount = a.InvoiceAmount,
                Status = a.Status.ToString(),   
                RejectionReason = a.RejectionReason,
                CreatedAt = a.CreatedAt
            }).ToList();
        }

        public async Task<List<CustomerPolicyDTO>> GetMyPoliciesAsync(int customerId)
        {
            var policies = await _policyRepository.GetPoliciesByCustomerIdAsync(customerId);

            // When the customer accesses their policies, update any policies whose
            // current year has ended and are within the 30-day payment window but
            // payment for the next year hasn't been made yet. Mark those as PendingPayment
            // so the UI can prompt the user to pay. We avoid aggressive cancellation here;
            // cancellation is enforced when a user attempts payment after the grace period.
            foreach (var pol in policies)
            {
                try
                {
                    if (pol.Status == PolicyStatus.Active && pol.CurrentYearNumber < pol.SelectedYears)
                    {
                        var now = DateTime.UtcNow;
                        // If current year ended and we are within the 30-day payment window
                        if (now > pol.CurrentYearEndDate && now <= pol.CurrentYearEndDate.AddDays(30))
                        {
                            if (pol.Status != PolicyStatus.PendingPayment)
                            {
                                pol.Status = PolicyStatus.PendingPayment;
                                await _policyRepository.UpdateAsync(pol);
                                await _notificationService.CreateNotificationAsync(pol.CustomerId, "Premium Payment Due", $"The premium payment for policy {pol.PolicyNumber} is now due. Please pay within the 30-day grace period.", NotificationType.PremiumPaymentDue, "Policy", pol.PolicyId.ToString());
                            }

                        }
                    }
                }
                catch
                {
                    // swallow update errors so listing still returns results
                }
            }

            var result = new List<CustomerPolicyDTO>();
            foreach (var p in policies)
            {
                var isTransfer = p.Vehicle?.VehicleApplication?.IsTransfer == true;
                bool isFeePending = false;

                if (isTransfer && p.Status == PolicyStatus.PendingPayment)
                {
                    var payments = await _paymentRepository.GetByPolicyIdAsync(p.PolicyId);
                    isFeePending = payments == null || !payments.Any();
                }

                result.Add(new CustomerPolicyDTO
                {
                    PolicyId = p.PolicyId,
                    PolicyNumber = p.PolicyNumber,
                    PlanName = p.Plan?.PlanName ?? "N/A",
                    VehicleRegistrationNumber = p.Vehicle?.RegistrationNumber ?? "N/A",
                    VehicleModel = p.Vehicle != null ? p.Vehicle.Make + " " + p.Vehicle.Model : "N/A",
                    PremiumAmount = isFeePending ? 500 : p.PremiumAmount,
                    IDV = p.IDV,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    Status = p.Status.ToString(),
                    IsRenewed = p.IsRenewed,
                    IsFeePending = isFeePending,
                    VehicleType = p.Vehicle?.VehicleType ?? "N/A",
                    RoadsideAssistanceAvailable = p.Plan?.RoadsideAssistanceAvailable ?? false
                });
            }

            return result;
        }

        public async Task<string> PayAnnualPremiumAsync(int policyId, int customerId, decimal? amountOverride = null, PaymentMethod paymentMethod = PaymentMethod.NetBanking, string? transactionReference = null)
        {
            var policy = await _policyRepository.GetByIdAsync(policyId);

            if (policy == null || policy.CustomerId != customerId)
                throw new NotFoundException("Policy not found");

            if (policy.Status == PolicyStatus.Cancelled)
                throw new BadRequestException("Policy is cancelled");

            // ==========================
            // DETECT TRANSFER FEE PAYMENT
            // ==========================
            var payments = await _paymentRepository.GetByPolicyIdAsync(policyId);
            bool isTransferFee = policy.Vehicle?.VehicleApplication?.IsTransfer == true && (payments == null || !payments.Any());

            if (isTransferFee)
            {
                var feePayment = new Payment
                {
                    PolicyId = policy.PolicyId,
                    Amount = amountOverride ?? 500, // Fixed Transfer Fee
                    PaymentDate = DateTime.UtcNow,
                    Status = PaymentStatus.Paid,
                    TransactionReference = transactionReference ?? "Transfer Fee",
                    PaymentMethod = paymentMethod
                };

                // After paying the fee, if the current year was already paid by the old owner, 
                // the policy becomes Active. Otherwise, it stays PendingPayment for the premium.
                if (policy.IsCurrentYearPaid)
                {
                    policy.Status = PolicyStatus.Active;
                }
                
                await _paymentRepository.AddAsync(feePayment);
                await _policyRepository.UpdateAsync(policy);
                await _auditService.LogActionAsync("PaymentSuccessful", "Payment", $"Transfer fee paid for policy: {policy.PolicyNumber}", "Policy", policy.PolicyId.ToString());
 
                return "Transfer fee paid successfully. Policy activated in your name.";
            }

            // ==========================
            // NORMAL PREMIUM PAYMENTS
            // FIRST PAYMENT CASE (NON-TRANSFER)
            if (policy.CurrentYearNumber == 0)
            {
                var firstPaymentPricingDto = new CalculateQuoteDTO
                {
                    InvoiceAmount = policy.InvoiceAmount,
                    ManufactureYear = policy.Vehicle.Year,
                    FuelType = policy.Vehicle.FuelType,
                    VehicleType = policy.Vehicle.VehicleType,
                    KilometersDriven = policy.InitialKilometersDriven,
                    PolicyYears = policy.SelectedYears,
                    PlanId = policy.PlanId
                };
                var result = _pricingService.CalculateAnnualPremium(
                    firstPaymentPricingDto,
                    policy.Plan,
                    false
                );

                var payment = new Payment
                {
                    PolicyId = policy.PolicyId,
                    Amount = amountOverride ?? result.Premium,
                    PaymentDate = DateTime.UtcNow,
                    Status = PaymentStatus.Paid,
                    TransactionReference = transactionReference ?? "Premium",
                    PaymentMethod = paymentMethod
                };
                policy.Status = PolicyStatus.Active;
                policy.CurrentYearNumber = 1;
                policy.StartDate = DateTime.UtcNow;
                policy.EndDate = policy.StartDate.AddYears(policy.SelectedYears);
                policy.CurrentYearEndDate = policy.StartDate.AddYears(1);
                policy.IsCurrentYearPaid = true;

                // persist payment record
                await _paymentRepository.AddAsync(payment);

                await _policyRepository.UpdateAsync(policy);
                await _auditService.LogActionAsync("PaymentSuccessful", "Payment", $"First premium paid for policy: {policy.PolicyNumber}", "Policy", policy.PolicyId.ToString());
 
                return "First payment successful. Policy activated.";
            }

            // SUBSEQUENT PAYMENTS
            // If we're past the 30-day payment window after the current year end, cancel
            if (DateTime.UtcNow > policy.CurrentYearEndDate.AddDays(30))
            {
                policy.Status = PolicyStatus.Cancelled;
                policy.CancellationDate = DateTime.UtcNow;
                await _policyRepository.UpdateAsync(policy);
                await _auditService.LogActionAsync("PolicyCancelled", "Policy", $"Policy {policy.PolicyNumber} cancelled due to non-payment.", "Policy", policy.PolicyId.ToString());
 
                throw new BadRequestException("Policy cancelled due to non-payment");
            }

            // If contract already completed, prompt renewal
            if (policy.CurrentYearNumber >= policy.SelectedYears)
                throw new BadRequestException("Contract completed. Please renew.");

            // Payments are only allowed within the 30-day window after the current year end.
            // Disallow early payments that would advance the policy year prematurely.
            if (DateTime.UtcNow < policy.CurrentYearEndDate)
            {
                throw new BadRequestException($"Payment not due yet. Next payment window opens on {policy.CurrentYearEndDate:yyyy-MM-dd} and is valid for 30 days.");
            }

            var subsequentPaymentPricingDto = new CalculateQuoteDTO
            {
                InvoiceAmount = policy.InvoiceAmount,
                ManufactureYear = policy.Vehicle.Year,
                FuelType = policy.Vehicle.FuelType,
                VehicleType = policy.Vehicle.VehicleType,
                KilometersDriven = policy.InitialKilometersDriven,
                PolicyYears = policy.SelectedYears,
                PlanId = policy.PlanId
            };

            var annualResult = _pricingService.CalculateAnnualPremium(
                subsequentPaymentPricingDto,
                policy.Plan,
                false
            );

            var newPayment = new Payment
            {
                PolicyId = policy.PolicyId,
                Amount = amountOverride ?? annualResult.Premium,
                PaymentDate = DateTime.UtcNow,
                Status = PaymentStatus.Paid,
                TransactionReference = transactionReference ?? "Premium",
                PaymentMethod = paymentMethod
            };

            policy.CurrentYearNumber++;
            policy.CurrentYearEndDate = policy.CurrentYearEndDate.AddYears(1);
            policy.IsCurrentYearPaid = true;
            policy.Status = PolicyStatus.Active; // Reactivate from PendingPayment
            
            // persist payment record
            await _paymentRepository.AddAsync(newPayment);

            await _policyRepository.UpdateAsync(policy);
            await _auditService.LogActionAsync("PaymentSuccessful", "Payment", $"Annual premium paid for policy: {policy.PolicyNumber}", "Policy", policy.PolicyId.ToString());
 
            return "Annual payment successful.";
        }

        public async Task<string> RenewPolicyAsync(int policyId, RenewPolicyDTO dto, int customerId)
        {
            var policy = await _policyRepository.GetByIdAsync(policyId);

            if (policy == null || policy.CustomerId != customerId)
                throw new NotFoundException("Policy not found");

            if (policy.IsRenewed)
                throw new BadRequestException("Already renewed");

            // Allow renewal starting 30 days before the end date through 30 days after the end date
            if (DateTime.UtcNow < policy.EndDate.AddDays(-30) ||
                DateTime.UtcNow > policy.EndDate.AddDays(30))
                throw new BadRequestException("Renewal is allowed only within 30 days before or after the policy end date");

            var newPlan = await _policyPlanService.GetPolicyPlanAsync(dto.NewPlanId);

            var pricingDto = new CalculateQuoteDTO
            {
                InvoiceAmount = policy.InvoiceAmount,
                ManufactureYear = policy.Vehicle.Year,
                FuelType = policy.Vehicle.FuelType,
                VehicleType = policy.Vehicle.VehicleType,
                KilometersDriven = policy.InitialKilometersDriven,
                PolicyYears = dto.SelectedYears,
                PlanId = dto.NewPlanId
            };

            var pricing = _pricingService.CalculateAnnualPremium(
                pricingDto,
                newPlan,
                true
            );

            var newPolicy = new Policy
            {
                PolicyNumber = $"POL-{DateTime.UtcNow.Year}-{Guid.NewGuid().ToString()[..6]}",
                CustomerId = customerId,
                VehicleId = policy.VehicleId,
                PlanId = dto.NewPlanId,
                Status = PolicyStatus.Active,
                StartDate = policy.EndDate,
                EndDate = policy.EndDate.AddYears(dto.SelectedYears),
                PremiumAmount = pricing.Premium,
                InvoiceAmount = policy.InvoiceAmount,
                IDV = pricing.IDV,
                SelectedYears = dto.SelectedYears,
                CurrentYearNumber = 1,
                CurrentYearEndDate = policy.EndDate.AddYears(1),
                IsCurrentYearPaid = true
            };

            // Prevent renewal if unpaid balances exist
            var hasUnpaid = await _paymentRepository.HasUnpaidAsync(policy.PolicyId);
            if (hasUnpaid)
                throw new BadRequestException("Cannot renew policy with unpaid balances.");

            // Add new policy and mark old policy expired transactionally
            await _policyRepository.AddAndExpireAsync(newPolicy, policy);
            await _auditService.LogActionAsync("PolicyRenewed", "Policy", $"Policy {policy.PolicyNumber} renewed to {newPolicy.PolicyNumber}", "Policy", newPolicy.PolicyId.ToString());
 
            return "Policy renewed successfully.";
        }

        // ============================================================
        // POLICY TRANSFER
        // ============================================================

        public async Task<string> InitiateTransferAsync(InitiateTransferDTO dto, int senderCustomerId)
        {
            // Validate policy belongs to sender and is Active
            var policy = await _policyRepository.GetByIdAsync(dto.PolicyId);
            if (policy == null || policy.CustomerId != senderCustomerId)
                throw new NotFoundException("Policy not found.");
            if (policy.Status != PolicyStatus.Active)
                throw new BadRequestException("Only active policies can be transferred.");

            // Check if there are any active (unresolved) claims associated with the policy
            var hasActiveClaims = await _claimsRepository.ExistsActiveClaimForPolicyAsync(dto.PolicyId);
            if (hasActiveClaims)
                throw new BadRequestException("Cannot transfer a policy with unresolved (active) claims.");

            // Check there is no existing pending transfer for this policy
            var existingTransfers = await _policyTransferRepository.GetBySenderIdAsync(senderCustomerId);
            if (existingTransfers.Any(t => t.PolicyId == dto.PolicyId
                && (t.Status == PolicyTransferStatus.PendingRecipientAcceptance
                 || t.Status == PolicyTransferStatus.PendingAgentApproval)))
                throw new BadRequestException("A transfer request is already in progress for this policy.");

            // Validate recipient exists and is a Customer
            var recipient = await _userRepository.GetByEmailAsync(dto.RecipientEmail);
            if (recipient == null || recipient.Role != UserRole.Customer)
                return "RECIPIENT_NOT_FOUND";

            if (recipient.UserId == senderCustomerId)
                throw new BadRequestException("You cannot transfer a policy to yourself.");

            var transfer = new PolicyTransfer
            {
                PolicyId = dto.PolicyId,
                SenderCustomerId = senderCustomerId,
                RecipientCustomerId = recipient.UserId,
                Status = PolicyTransferStatus.PendingRecipientAcceptance,
                CreatedAt = DateTime.UtcNow
            };

            await _policyTransferRepository.AddAsync(transfer);
            await _policyTransferRepository.SaveChangesAsync();
            await _auditService.LogActionAsync("PolicyTransferInitiated", "Policy", $"Transfer initiated for policy {policy.PolicyNumber} to {dto.RecipientEmail}", "PolicyTransfer", transfer.PolicyTransferId.ToString());
            
            // Notify Recipient
            await _notificationService.CreateNotificationAsync(recipient.UserId, "Policy Transfer Request", $"You have received a policy transfer request for {policy.Vehicle?.RegistrationNumber} from {policy.Customer?.FullName}.", NotificationType.PolicyTransferStatusChanged, "PolicyTransfer", transfer.PolicyTransferId.ToString());
            
            return "TRANSFER_INITIATED";

        }

        public async Task<List<object>> GetMyIncomingTransfersAsync(int recipientCustomerId)
        {
            var transfers = await _policyTransferRepository.GetByRecipientIdAsync(recipientCustomerId);
            return transfers.Select(t => (object)new
            {
                t.PolicyTransferId,
                Status = t.Status.ToString(),
                t.CreatedAt,
                SenderName = t.SenderCustomer?.FullName,
                SenderEmail = t.SenderCustomer?.Email,
                Policy = t.Policy == null ? null : new
                {
                    t.Policy.PolicyId,
                    t.Policy.PolicyNumber,
                    t.Policy.PremiumAmount,
                    t.Policy.IDV,
                    PlanName = t.Policy.Plan?.PlanName,
                    Vehicle = t.Policy.Vehicle == null ? null : new
                    {
                        t.Policy.Vehicle.RegistrationNumber,
                        t.Policy.Vehicle.Make,
                        t.Policy.Vehicle.Model,
                        t.Policy.Vehicle.Year
                    }
                }
            }).ToList();
        }

        public async Task<List<object>> GetMyOutgoingTransfersAsync(int senderCustomerId)
        {
            var transfers = await _policyTransferRepository.GetBySenderIdAsync(senderCustomerId);
            return transfers.Select(t => (object)new
            {
                t.PolicyTransferId,
                Status = t.Status.ToString(),
                t.CreatedAt,
                RecipientName = t.RecipientCustomer?.FullName,
                RecipientEmail = t.RecipientCustomer?.Email,
                Policy = t.Policy == null ? null : new
                {
                    t.Policy.PolicyId,
                    t.Policy.PolicyNumber,
                    Vehicle = t.Policy.Vehicle == null ? null : new
                    {
                        t.Policy.Vehicle.RegistrationNumber,
                        t.Policy.Vehicle.Make,
                        t.Policy.Vehicle.Model
                    }
                }
            }).ToList();
        }

        public async Task<string> AcceptTransferAsync(int transferId, IFormFile rcDocument, int recipientCustomerId)
        {
            var transfer = await _policyTransferRepository.GetByIdAsync(transferId);
            if (transfer == null || transfer.RecipientCustomerId != recipientCustomerId)
                throw new NotFoundException("Transfer request not found.");
            if (transfer.Status != PolicyTransferStatus.PendingRecipientAcceptance)
                throw new BadRequestException("This transfer is no longer awaiting your acceptance.");

            var policy = transfer.Policy;
            var vehicle = policy.Vehicle;
            var oldApp = vehicle.VehicleApplication;

            // Define structure: user_{recipientId}/{vehicleId}/transfer_policies/transfer_{transferId}
            string storageIdentifier = $"{recipientCustomerId}/{vehicle.VehicleId}/transfer_policies/transfer_{transferId}";

            // Save new RC document into the recipient's user folder structure
            var rcRelative = await _fileStorageService.SaveFileAsync(rcDocument, "user", storageIdentifier, "rc");

            // Prepare new application
            var newApp = new VehicleApplication
            {
                CustomerId = recipientCustomerId,
                AssignedAgentId = policy.AgentId ?? (await _userRepository.GetLeastLoadedAgentAsync())?.UserId,
                PlanId = policy.PlanId,
                RegistrationNumber = vehicle.RegistrationNumber,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                FuelType = vehicle.FuelType,
                VehicleType = vehicle.VehicleType,
                KilometersDriven = oldApp?.KilometersDriven ?? 0,
                PolicyYears = policy.SelectedYears,
                InvoiceAmount = policy.InvoiceAmount,
                Status = VehicleApplicationStatus.UnderReview,
                IsTransfer = true,
                CreatedAt = DateTime.UtcNow,
                Documents = new List<VehicleDocument>
                {
                    new VehicleDocument { DocumentType = "RC", FilePath = rcRelative, UploadedAt = DateTime.UtcNow }
                }
            };

            // Copy previous customer's invoice document into the new transfer folder
            var oldInvoicePath = oldApp?.Documents?.FirstOrDefault(d => d.DocumentType == "Invoice")?.FilePath;
            if (!string.IsNullOrEmpty(oldInvoicePath))
            {
                var newInvoicePath = await _fileStorageService.CopyFileAsync(oldInvoicePath, "user", storageIdentifier, "invoice");
                if (!string.IsNullOrEmpty(newInvoicePath))
                {
                    newApp.Documents.Add(new VehicleDocument { DocumentType = "Invoice", FilePath = newInvoicePath, UploadedAt = DateTime.UtcNow });
                }
            }

            await _vehicleApplicationRepository.AddAsync(newApp);
            await _vehicleApplicationRepository.SaveChangesAsync();

            // Advance transfer status
            transfer.Status = PolicyTransferStatus.PendingAgentApproval;
            transfer.NewVehicleApplicationId = newApp.VehicleApplicationId;
            transfer.UpdatedAt = DateTime.UtcNow;
            await _policyTransferRepository.SaveChangesAsync();
            await _auditService.LogActionAsync("PolicyTransferAccepted", "Policy", $"Recipient accepted transfer for policy {policy.PolicyNumber}", "PolicyTransfer", transfer.PolicyTransferId.ToString());
            
            // Notify Sender
            await _notificationService.CreateNotificationAsync(transfer.SenderCustomerId, "Transfer Request Accepted", $"Your transfer request for policy {policy.PolicyNumber} has been accepted by the recipient and is now pending agent approval.", NotificationType.PolicyTransferStatusChanged, "PolicyTransfer", transfer.PolicyTransferId.ToString());
            
            // Notify Agent
            if (newApp.AssignedAgentId.HasValue)
            {
                await _notificationService.CreateNotificationAsync(newApp.AssignedAgentId.Value, "New Policy Transfer Assigned", $"A new policy transfer request for {newApp.RegistrationNumber} has been assigned to you for review.", NotificationType.NewPolicyRequestAssigned, "VehicleApplication", newApp.VehicleApplicationId.ToString());
            }

            return "TRANSFER_ACCEPTED";

        }

        public async Task<string> RejectTransferAsync(int transferId, int recipientCustomerId)
        {
            var transfer = await _policyTransferRepository.GetByIdAsync(transferId);
            if (transfer == null || transfer.RecipientCustomerId != recipientCustomerId)
                throw new NotFoundException("Transfer request not found.");
            if (transfer.Status != PolicyTransferStatus.PendingRecipientAcceptance)
                throw new BadRequestException("This transfer is not awaiting your response.");

            transfer.Status = PolicyTransferStatus.RejectedByRecipient;
            transfer.UpdatedAt = DateTime.UtcNow;
            await _policyTransferRepository.SaveChangesAsync();
            await _auditService.LogActionAsync("PolicyTransferRejected", "Policy", $"Recipient rejected transfer for policy {transfer.PolicyId}", "PolicyTransfer", transfer.PolicyTransferId.ToString());
            
            // Notify Sender
            await _notificationService.CreateNotificationAsync(transfer.SenderCustomerId, "Transfer Request Rejected", $"Your transfer request for policy {transfer.PolicyId} has been rejected by the recipient.", NotificationType.PolicyTransferStatusChanged, "PolicyTransfer", transfer.PolicyTransferId.ToString());

            return "TRANSFER_REJECTED";

        }
    }
}
