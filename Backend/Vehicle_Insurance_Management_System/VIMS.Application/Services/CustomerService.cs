using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        public CustomerService(ICustomerRepository customerRepository,IVehicleApplicationRepository vehicleApplicationRepository,IUserRepository userRepository,IVehicleRepository vehicleRepository,IPolicyRepository policyRepository,IPaymentRepository paymentRepository,IPricingService pricingService,IPolicyPlanService policyPlanService)
        {
            _customerRepository = customerRepository;
            _vehicleApplicationRepository = vehicleApplicationRepository;
            _userRepository = userRepository;
            _vehicleRepository = vehicleRepository;
            _policyRepository = policyRepository;
            _paymentRepository = paymentRepository;
            _pricingService = pricingService;
            _policyPlanService = policyPlanService;
        }
        public async Task<List<PolicyPlan>> ViewAllPoliciesAsync()
        {
            return await _customerRepository.ViewAllPolicyPlansAsync();
        }
        public async Task CreateApplicationAsync(CreateVehicleApplicationDTO dto,int userId)
        {
            // ==============================
            // 1️⃣ Validate Registration Number
            // ==============================

            var regNumber = dto.RegistrationNumber.ToUpper().Replace(" ", "").Replace("-", "");

            var regex = new Regex(@"^[A-Z]{2}\d{1,2}[A-Z]{1,2}\d{1,4}$");

            if (!regex.IsMatch(regNumber))
                throw new BadRequestException("Invalid vehicle registration number format.");

            // ==============================
            // 2️⃣ Check if vehicle already insured
            // ==============================

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

            // Save Uploaded Files

            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

            var userFolder = Path.Combine(basePath, $"user_{userId}");
            var claimsFolder = Path.Combine(userFolder, "claimsdocuments");
            var policyFolder = Path.Combine(userFolder, "policydocuments");

            // New subfolders
            var invoiceFolder = Path.Combine(policyFolder, "invoice");
            var rcFolder = Path.Combine(policyFolder, "rc");

            // ==========================
            // Create Folders If Not Exists
            // ==========================

            Directory.CreateDirectory(userFolder);
            Directory.CreateDirectory(claimsFolder);
            Directory.CreateDirectory(policyFolder);
            Directory.CreateDirectory(invoiceFolder);
            Directory.CreateDirectory(rcFolder);

            // ==========================
            // Save Invoice File
            // ==========================

            if (dto.InvoiceDocument != null)
            {
                var invoiceFileName = Guid.NewGuid() + "_" + dto.InvoiceDocument.FileName;
                var invoiceFullPath = Path.Combine(invoiceFolder, invoiceFileName);

                using (var stream = new FileStream(invoiceFullPath, FileMode.Create))
                {
                    await dto.InvoiceDocument.CopyToAsync(stream);
                }

                application.Documents.Add(new VehicleDocument
                {
                    DocumentType = "Invoice",
                    FilePath = Path.Combine("uploads", $"user_{userId}", "policydocuments", "invoice", invoiceFileName).Replace("\\", "/")
                });
            }

            // ==========================
            // Save RC File
            // ==========================

            if (dto.RcDocument != null)
            {
                var rcFileName = Guid.NewGuid() + "_" + dto.RcDocument.FileName;
                var rcFullPath = Path.Combine(rcFolder, rcFileName);

                using (var stream = new FileStream(rcFullPath, FileMode.Create))
                {
                    await dto.RcDocument.CopyToAsync(stream);
                }

                application.Documents.Add(new VehicleDocument
                {
                    DocumentType = "RC",
                    FilePath = Path.Combine("uploads", $"user_{userId}", "policydocuments", "rc", rcFileName).Replace("\\", "/")
                });
            }

            await _vehicleApplicationRepository.AddAsync(application);
            await _vehicleApplicationRepository.SaveChangesAsync();
        }

        public async Task<List<CustomerApplicationDTO>> GetMyApplicationsAsync(int customerId)
        {
            var apps = await _vehicleApplicationRepository.GetByCustomerIdAsync(customerId);

            return apps.Select(a => new CustomerApplicationDTO
            {
                VehicleApplicationId = a.VehicleApplicationId,
                RegistrationNumber = a.RegistrationNumber,
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
                            }
                        }
                    }
                }
                catch
                {
                    // swallow update errors so listing still returns results
                }
            }

            return policies.Select(p => new CustomerPolicyDTO
            {
                PolicyId = p.PolicyId,
                PolicyNumber = p.PolicyNumber,
                PlanName = p.Plan?.PlanName ?? "N/A",
                VehicleRegistrationNumber = p.Vehicle?.RegistrationNumber ?? "N/A",
                VehicleModel = p.Vehicle != null
          ? p.Vehicle.Make + " " + p.Vehicle.Model
          : "N/A",
                PremiumAmount = p.PremiumAmount,
                IDV = p.IDV,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Status = p.Status.ToString()
            }).ToList();
        }

        public async Task<string> PayAnnualPremiumAsync(int policyId, int customerId)
        {
            var policy = await _policyRepository.GetByIdAsync(policyId);

            if (policy == null || policy.CustomerId != customerId)
                throw new NotFoundException("Policy not found");

            if (policy.Status == PolicyStatus.Cancelled)
                throw new BadRequestException("Policy is cancelled");

            // FIRST PAYMENT CASE
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
                    Amount = result.Premium,
                    PaymentDate = DateTime.UtcNow,
                    Status = PaymentStatus.Paid,
                    TransactionReference = "Premium"
                };
                policy.Status = PolicyStatus.Active;
                policy.CurrentYearNumber = 1;
                policy.CurrentYearEndDate = policy.StartDate.AddYears(1);
                policy.IsCurrentYearPaid = true;

                // persist payment record
                await _paymentRepository.AddAsync(payment);

                await _policyRepository.UpdateAsync(policy);

                return "First payment successful. Policy activated.";
            }

            // SUBSEQUENT PAYMENTS
            // If we're past the 30-day payment window after the current year end, cancel
            if (DateTime.UtcNow > policy.CurrentYearEndDate.AddDays(30))
            {
                policy.Status = PolicyStatus.Cancelled;
                policy.CancellationDate = DateTime.UtcNow;
                await _policyRepository.UpdateAsync(policy);

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
                Amount = annualResult.Premium,
                PaymentDate = DateTime.UtcNow,
                Status = PaymentStatus.Paid,
                TransactionReference = "Premium"
            };

            policy.CurrentYearNumber++;
            policy.CurrentYearEndDate = policy.CurrentYearEndDate.AddYears(1);
            policy.IsCurrentYearPaid = true;
            policy.Status = PolicyStatus.Active; // Reactivate from PendingPayment
            
            // persist payment record
            await _paymentRepository.AddAsync(newPayment);

            await _policyRepository.UpdateAsync(policy);

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

            return "Policy renewed successfully.";
        }
    }
}
