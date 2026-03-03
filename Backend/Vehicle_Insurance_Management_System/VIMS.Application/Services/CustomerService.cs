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
 
        public CustomerService(ICustomerRepository customerRepository, IVehicleApplicationRepository vehicleApplicationRepository, IUserRepository userRepository, IVehicleRepository vehicleRepository, IPolicyRepository policyRepository, IPaymentRepository paymentRepository, IPricingService pricingService, IPolicyPlanService policyPlanService, IPolicyTransferRepository policyTransferRepository, IAuditService auditService, IFileStorageService fileStorageService)
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

            // Save Uploaded Files via FileStorageService (Dependency Inversion Principle)
            if (dto.InvoiceDocument != null)
            {
                string invoicePath = await _fileStorageService.SaveFileAsync(dto.InvoiceDocument, "user", userId.ToString(), "invoice");
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
                string rcPath = await _fileStorageService.SaveFileAsync(dto.RcDocument, "user", userId.ToString(), "rc");
                if (!string.IsNullOrEmpty(rcPath))
                {
                    application.Documents.Add(new VehicleDocument
                    {
                        DocumentType = "RC",
                        FilePath = rcPath
                    });
                }
            }

            await _vehicleApplicationRepository.AddAsync(application);
            await _vehicleApplicationRepository.SaveChangesAsync();
            await _auditService.LogActionAsync("PolicyApplicationCreated", "Policy", $"Customer created a policy application for {application.Make} {application.Model}", "VehicleApplication", application.VehicleApplicationId.ToString());
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
                    Status = p.Status.ToString()
                });
            }

            return result;
        }

        public async Task<string> PayAnnualPremiumAsync(int policyId, int customerId)
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
                    Amount = 500, // Fixed Transfer Fee
                    PaymentDate = DateTime.UtcNow,
                    Status = PaymentStatus.Paid,
                    TransactionReference = "Transfer Fees"
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

            // Save RC document via FileStorageService
            var rcRelative = await _fileStorageService.SaveFileAsync(rcDocument, "transfer", transferId.ToString(), "rc");

            // Find the oldest document (InvoiceDoc) from original application to copy its path
            var invoicePath = oldApp?.Documents?.FirstOrDefault(d => d.DocumentType == "Invoice")?.FilePath ?? string.Empty;

            // Determine assigned agent (reuse same agent if possible)
            int? agentId = policy.AgentId;
            if (agentId == null)
            {
                var agent = await _userRepository.GetLeastLoadedAgentAsync();
                agentId = agent?.UserId;
            }

            // Create new VehicleApplication for recipient
            var newApp = new VehicleApplication
            {
                CustomerId = recipientCustomerId,
                AssignedAgentId = agentId,
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

            // Copy invoice document if it exists
            if (!string.IsNullOrEmpty(invoicePath))
                newApp.Documents.Add(new VehicleDocument { DocumentType = "Invoice", FilePath = invoicePath, UploadedAt = DateTime.UtcNow });

            await _vehicleApplicationRepository.AddAsync(newApp);
            await _vehicleApplicationRepository.SaveChangesAsync();

            // Advance transfer status
            transfer.Status = PolicyTransferStatus.PendingAgentApproval;
            transfer.NewVehicleApplicationId = newApp.VehicleApplicationId;
            transfer.UpdatedAt = DateTime.UtcNow;
            await _policyTransferRepository.SaveChangesAsync();
            await _auditService.LogActionAsync("PolicyTransferAccepted", "Policy", $"Recipient accepted transfer for policy {policy.PolicyNumber}", "PolicyTransfer", transfer.PolicyTransferId.ToString());
 
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
 
            return "TRANSFER_REJECTED";
        }
    }
}
