using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Application.Exceptions;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.Application.Services
{
    public class ClaimsService : IClaimsService
    {
        private readonly IClaimsRepository _claimsRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPolicyRepository _policyRepository;
        private readonly IPricingService _pricingService;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IAuditService _auditService;
        private readonly IFileStorageService _fileStorageService;

        public ClaimsService(IClaimsRepository claimsRepository, IUserRepository userRepository, IPolicyRepository policyRepository, IPricingService pricingService, IPaymentRepository paymentRepository, IAuditService auditService, IFileStorageService fileStorageService)
        {
            _claimsRepository = claimsRepository;
            _userRepository = userRepository;
            _policyRepository = policyRepository;
            _pricingService = pricingService;
            _paymentRepository = paymentRepository;
            _auditService = auditService;
            _fileStorageService = fileStorageService;
        }

        public async Task<List<Claims>> GetAllClaimsAsync()
        {
            return await _claimsRepository.GetAllAsync();
        }

        public async Task<List<Claims>> GetClaimsByCustomerAsync(int customerId)
        {
            return await _claimsRepository.GetByCustomerIdAsync(customerId);
        }

        public async Task<string> SubmitClaimAsync(SubmitClaimDTO dto, int customerId)
        {
            var policy = await _policyRepository.GetByIdAsync(dto.PolicyId);
            if (policy == null || policy.CustomerId != customerId)
                throw new NotFoundException("Policy not found");

            // Prevent multiple active (submitted) claims for the same policy
            var hasActive = await _claimsRepository.ExistsActiveClaimForPolicyAsync(dto.PolicyId);
            if (hasActive)
                throw new BadRequestException("An active claim already exists for this policy. Please wait for it to be processed before submitting another.");

            var plan = policy.Plan;
            var parsedClaimType = dto.ClaimType;

            // Verify coverage
            if (parsedClaimType == VIMS.Domain.Enums.ClaimType.Theft && !plan.CoversTheft)
                throw new BadRequestException("This policy does not cover theft claims");
            if (parsedClaimType == VIMS.Domain.Enums.ClaimType.Damage && !plan.CoversOwnDamage)
                throw new BadRequestException("This policy does not cover own damage claims");
            if (parsedClaimType == VIMS.Domain.Enums.ClaimType.ThirdParty && !plan.CoversThirdParty)
                throw new BadRequestException("This policy does not cover third-party claims");


            var claim = new Claims
            {
                ClaimNumber = $"CLM-{DateTime.UtcNow.Year}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                PolicyId = dto.PolicyId,
                CustomerId = customerId,
                claimType = parsedClaimType,
                Status = ClaimStatus.Submitted
            };

            // assign claims officer: find least loaded claims officer and set before create
            var officer = await _userRepository.GetLeastLoadedClaimsOfficerAsync();
            if (officer != null)
                claim.ClaimsOfficerId = officer.UserId;

            // create claim (this will generate ClaimId)
            var created = await _claimsRepository.AddAsync(claim);

            // store documents according to claim type via FileStorageService
            string? doc1Path = null;
            string? doc2Path = null;
            string claimIdentifier = $"claim_{created.ClaimId}";

            if (parsedClaimType == VIMS.Domain.Enums.ClaimType.Theft)
            {
                if (dto.Document1 == null)
                    throw new BadRequestException("FIR is required for theft claims");

                doc1Path = await _fileStorageService.SaveFileAsync(dto.Document1, "user", customerId.ToString(), $"claimsdocuments/{claimIdentifier}");
            }
            else if (parsedClaimType == VIMS.Domain.Enums.ClaimType.Damage)
            {
                // need repair bill and invoice
                if (dto.Document1 == null)
                    throw new BadRequestException("Repair bill is required for own damage claims");

                doc1Path = await _fileStorageService.SaveFileAsync(dto.Document1, "user", customerId.ToString(), $"claimsdocuments/{claimIdentifier}");

                // Assuming user has uploaded an invoice in policy documents,
                // but for simplicity, we fallback to dto.Document2
                if (doc2Path == null && dto.Document2 != null)
                {
                    doc2Path = await _fileStorageService.SaveFileAsync(dto.Document2, "user", customerId.ToString(), $"claimsdocuments/{claimIdentifier}");
                }

                if (doc2Path == null)
                    throw new BadRequestException("Invoice required for own damage claims");
            }
            else if (parsedClaimType == VIMS.Domain.Enums.ClaimType.ThirdParty)
            {
                // need invoice of the car and repair bill of that car
                if (dto.Document1 == null || dto.Document2 == null)
                    throw new BadRequestException("Both repair bill and vehicle invoice are required for third party claims");

                doc1Path = await _fileStorageService.SaveFileAsync(dto.Document1, "user", customerId.ToString(), $"claimsdocuments/{claimIdentifier}");
                doc2Path = await _fileStorageService.SaveFileAsync(dto.Document2, "user", customerId.ToString(), $"claimsdocuments/{claimIdentifier}");
            }

            var claimDoc = new ClaimDocument
            {
                ClaimId = created.ClaimId,
                Document1 = doc1Path ?? string.Empty,
                Document2 = doc2Path ?? string.Empty
            };

            await _claimsRepository.AddDocumentAsync(claimDoc);
            await _auditService.LogActionAsync("ClaimSubmitted", "Claim", $"User submitted claim: {created.ClaimNumber}", "Claim", created.ClaimId.ToString());

            return "Claim submitted";
        }

        public async Task<string> DecideClaimAsync(int claimId, ApproveClaimDTO dto, int officerId, bool approve)
        {
            var claim = await _claimsRepository.GetByIdAsync(claimId);
            if (claim == null)
                throw new NotFoundException("Claim not found");

            if (claim.ClaimsOfficerId != officerId)
                throw new BadRequestException("Not authorized to decide this claim");

            if (!approve)
            {
                if (string.IsNullOrWhiteSpace(dto.RejectionReason))
                    throw new BadRequestException("Please provide a reason for rejecting this claim.");

                claim.Status = ClaimStatus.Rejected;
                claim.RejectionReason = dto.RejectionReason;
                await _claimsRepository.UpdateAsync(claim);
                await _auditService.LogActionAsync("ClaimRejected", "Claim", $"Officer rejected claim: {claim.ClaimNumber}. Reason: {dto.RejectionReason}", "Claim", claim.ClaimId.ToString());
                return "Claim rejected";
            }

            // Approval flow: add the reason if provided even on approval
            claim.RejectionReason = dto.RejectionReason; 
            // ensure navigation properties are loaded
            var policy = claim.Policy ?? await _policyRepository.GetByIdAsync(claim.PolicyId);
            if (policy == null)
                throw new NotFoundException("Policy not found for claim");

            var plan = policy.Plan;
            if (plan == null)
            {
                var fullPolicy = await _policyRepository.GetByIdAsync(policy.PolicyId);
                plan = fullPolicy?.Plan;
            }

            if (plan == null)
                throw new NotFoundException("Policy plan not found");

            decimal payout = 0m;

            // compute insured vehicle IDV at time of claim (do not use stored Policy.IDV)
            // ensure vehicle is available
            var insuredVehicle = policy.Vehicle ?? (await _policyRepository.GetByIdAsync(policy.PolicyId))?.Vehicle;
            if (insuredVehicle == null)
                throw new NotFoundException("Vehicle not found for policy");

            var insuredIdv = _pricingService.CalculateIDV(policy.InvoiceAmount, insuredVehicle.Year);

            if (claim.claimType == ClaimType.Theft)
            {
                // pay full IDV computed at time of claim
                payout = insuredIdv;
                // invoice/document paths are available via claim.Documents or policy documents
            }
            else if (claim.claimType == ClaimType.Damage)
            {
                if (dto.RepairCost == null)
                    throw new BadRequestException("Repair cost required");

                var repair = dto.RepairCost.Value;
                decimal engine = dto.EngineCost ?? 0m;

                // depreciation unless zero depreciation selected
                decimal depreciationFactor = 0.10m; // example 10% depreciation
                if (plan.ZeroDepreciationAvailable)
                    depreciationFactor = 0m;

                decimal depreciatedRepair = repair - (repair * depreciationFactor);
                decimal depreciatedEngine = engine - (engine * depreciationFactor);

                payout = depreciatedRepair + depreciatedEngine;

                // remove deductible
                payout -= plan.DeductibleAmount;
                if (payout < 0) payout = 0;
            }
            else // ThirdParty
            {
                if (dto.RepairCost == null || dto.InvoiceAmount == null || dto.ManufactureYear == null)
                    throw new BadRequestException("Repair cost, invoice amount and manufacture year required for third party claims");

                var repair = dto.RepairCost.Value;
                var invoice = dto.InvoiceAmount.Value;

                // compute idv using shared pricing service
                var idv = _pricingService.CalculateIDV(invoice, dto.ManufactureYear.Value);

                if (repair > idv * 0.75m)
                {
                    // large repair - pay up to IDV
                    payout = idv;
                }
                else
                {
                    // apply depreciation to repair
                    // Determine depreciation percent from age grid (recommended grid)
                    decimal depreciationPercent;
                    if (plan.ZeroDepreciationAvailable)
                    {
                        depreciationPercent = 0m;
                    }
                    else
                    {
                        int age = DateTime.UtcNow.Year - dto.ManufactureYear.Value;
                        depreciationPercent = age switch
                        {
                            0 => 0.05m, // 0-6 months approximated as 0
                            1 => 0.10m,
                            2 => 0.15m,
                            3 => 0.25m,
                            4 => 0.35m,
                            5 => 0.45m,
                            >= 6 and <= 7 => 0.55m, // 5-7 years
                            >= 8 and <= 10 => 0.65m, // 7-10 years
                            _ => 0.75m
                        };
                    }

                    payout = repair - (repair * depreciationPercent);
                }

                // remove deductible
                payout -= plan.DeductibleAmount;
                if (payout < 0) payout = 0;
            }

            // mark claim approved and store approved amount
            claim.Status = ClaimStatus.Approved;
            claim.ApprovedAmount = Math.Round(payout, 2);

            // determine decision type
            var decisionType = "Partial";
            if (claim.claimType == ClaimType.Theft && claim.ApprovedAmount >= insuredIdv)
                decisionType = "TotalLoss";
            else if ((claim.ApprovedAmount ?? 0m) >= (insuredIdv * 0.75m))
                decisionType = "ConstructiveTotalLoss";

            claim.DecisionType = decisionType;

            await _claimsRepository.UpdateAsync(claim);
            await _auditService.LogActionAsync("ClaimApproved", "Claim", $"Officer approved claim: {claim.ClaimNumber} with amount {claim.ApprovedAmount}", "Claim", claim.ClaimId.ToString());

            // increment policy claim count and decide if policy should be Claimed (constructive total loss)
            try
            {
                policy.ClaimCount += 1;

                // Recalculate IDV at claim time
                var currentIdv = _pricingService.CalculateIDV(policy.InvoiceAmount, policy.Vehicle?.Year ?? insuredVehicle.Year);

                // If approved amount >= IDV or approved >= 75% of IDV then set status to Claimed
                if ((claim.ApprovedAmount ?? 0m) >= currentIdv || (claim.ApprovedAmount ?? 0m) >= (currentIdv * 0.75m))
                {
                    policy.Status = PolicyStatus.Claimed;
                }

                await _policyRepository.UpdateAsync(policy);
            }
            catch
            {
                // ignore policy update errors here
            }

            // log payment record
            var payment = new Payment
            {
                PolicyId = claim.PolicyId,
                Amount = claim.ApprovedAmount ?? 0m,
                PaymentDate = DateTime.UtcNow,
                Status = PaymentStatus.Paid,
                TransactionReference = "Claim"
            };

            // create payment via injected repository
            await _paymentRepository.AddAsync(payment);
            await _auditService.LogActionAsync("PaymentSuccessful", "Payment", $"Claim payout paid for claim: {claim.ClaimNumber}", "Claim", claim.ClaimId.ToString());

            return "Claim approved";
        }
        private decimal CalculateIDV(decimal invoiceAmount, int manufactureYear)
        {
            int age = DateTime.UtcNow.Year - manufactureYear;

            decimal depreciation = age switch
            {
                <= 0 => 0.05m,
                1 => 0.15m,
                2 => 0.20m,
                3 => 0.30m,
                4 => 0.40m,
                5 => 0.50m,
                _ => 0.60m
            };

            return invoiceAmount - (invoiceAmount * depreciation);
        }
    }
}
