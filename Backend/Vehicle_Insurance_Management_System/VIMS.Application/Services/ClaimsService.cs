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

        public async Task<Claims?> GetClaimByIdAsync(int claimId)
        {
            return await _claimsRepository.GetByIdAsync(claimId);
        }

        public async Task<List<Claims>> GetClaimsByOfficerIdAsync(int officerId)
        {
            return await _claimsRepository.GetByOfficerIdAsync(officerId);
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


            if (parsedClaimType == VIMS.Domain.Enums.ClaimType.Theft && dto.Document1 == null)
                throw new BadRequestException("FIR is required for theft claims");

            if (parsedClaimType == VIMS.Domain.Enums.ClaimType.Damage)
            {
                if (dto.Document1 == null)
                    throw new BadRequestException("Repair bill is required for own damage claims");
                if (dto.Document2 == null && dto.Document1 == null) // Checking logically if invoice is strictly required initially, wait, previous code had: if (doc2Path == null && dto.Document2 != null) and then if doc2Path == null throw. Basically Document2 was required unless fallback. Simplest is to just check dto if that was the intent.
                    throw new BadRequestException("Invoice required for own damage claims");
            }

            if (parsedClaimType == VIMS.Domain.Enums.ClaimType.ThirdParty)
            {
                if (dto.Document1 == null || dto.Document2 == null)
                    throw new BadRequestException("Both repair bill and vehicle invoice are required for third party claims");
            }

            // assign claims officer: find least loaded claims officer and set before create
            var officer = await _userRepository.GetLeastLoadedClaimsOfficerAsync();

            var claim = new Claims
            {
                ClaimNumber = $"CLM-{DateTime.UtcNow.Year}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                PolicyId = dto.PolicyId,
                CustomerId = customerId,
                claimType = parsedClaimType,
                Status = ClaimStatus.Submitted,
                CreatedAt = DateTime.UtcNow
            };

            if (officer != null)
                claim.ClaimsOfficerId = officer.UserId;

            // create claim (this will generate ClaimId)
            var created = await _claimsRepository.AddAsync(claim);

            // store documents according to claim type via FileStorageService
            string? doc1Path = null;
            string? doc2Path = null;
            
            // New structure: {userId}/{vehicleId}/claims document/{claim_type}
            string vehicleIdStr = policy.VehicleId.ToString();
            string claimTypeFolder = parsedClaimType.ToString();
            string storageIdentifier = $"{customerId}/{vehicleIdStr}/claims document/{claimTypeFolder}";

            if (parsedClaimType == VIMS.Domain.Enums.ClaimType.Theft)
            {
                // For theft: Document1 is FIR
                doc1Path = await _fileStorageService.SaveFileAsync(dto.Document1, "user", storageIdentifier, "FIR");
            }
            else if (parsedClaimType == VIMS.Domain.Enums.ClaimType.Damage)
            {
                // For own damage: Document1 is Repair Bill, Document2 is Invoice
                doc1Path = await _fileStorageService.SaveFileAsync(dto.Document1, "user", storageIdentifier, "repair bill");
                
                if (dto.Document2 != null)
                {
                    // User uploaded a new invoice copy
                    doc2Path = await _fileStorageService.SaveFileAsync(dto.Document2, "user", storageIdentifier, "invoice");
                }
                else
                {
                    // Pull from the vehicle folder: {userId}/{vehicleId}/invoice
                    // We search in the vehicle's original application documents
                    var vehicleInvoice = policy.Vehicle?.VehicleApplication?.Documents?
                        .FirstOrDefault(d => d.DocumentType == "Invoice")?.FilePath;
                    
                    if (!string.IsNullOrEmpty(vehicleInvoice))
                    {
                        doc2Path = await _fileStorageService.CopyFileAsync(vehicleInvoice, "user", storageIdentifier, "invoice");
                    }
                }
            }
            else if (parsedClaimType == VIMS.Domain.Enums.ClaimType.ThirdParty)
            {
                // For third party: Document1 is Repair Bill, Document2 is Invoice
                doc1Path = await _fileStorageService.SaveFileAsync(dto.Document1, "user", storageIdentifier, "repair bill");
                doc2Path = await _fileStorageService.SaveFileAsync(dto.Document2, "user", storageIdentifier, "invoice");
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

            // compute insured vehicle IDV at time of claim (do not use stored Policy.IDV)
            // ensure vehicle is available
            var insuredVehicle = policy.Vehicle ?? (await _policyRepository.GetByIdAsync(policy.PolicyId))?.Vehicle;
            if (insuredVehicle == null)
                throw new NotFoundException("Vehicle not found for policy");

            var insuredIdv = _pricingService.CalculateIDV(policy.InvoiceAmount, insuredVehicle.Year);

            var breakdown = await CalculateClaimBreakdownAsync(claimId, dto);
            
            // mark claim approved and store approved amount
            claim.Status = ClaimStatus.Approved;
            claim.ApprovedAmount = breakdown.FinalPayout;

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
                 //ignore policy update errors here
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
        public async Task<ClaimBreakdownDTO> CalculateClaimBreakdownAsync(int claimId, ApproveClaimDTO dto)
        {
            var claim = await _claimsRepository.GetByIdAsync(claimId);
            if (claim == null) throw new NotFoundException("Claim not found");

            var policy = claim.Policy ?? await _policyRepository.GetByIdAsync(claim.PolicyId);
            var plan = policy?.Plan;
            var vehicle = policy?.Vehicle;

            if (policy == null || plan == null || vehicle == null)
                throw new BadRequestException("Missing policy, plan, or vehicle details");

            int currentYear = DateTime.UtcNow.Year;
            int age = currentYear - vehicle.Year;
            decimal insuredIdv = _pricingService.CalculateIDV(policy.InvoiceAmount, vehicle.Year);

            var breakdown = new ClaimBreakdownDTO
            {
                IDV = insuredIdv,
                Deductible = plan.DeductibleAmount,
                MaxCoverage = plan.MaxCoverageAmount
            };

            if (claim.claimType == ClaimType.Theft)
            {
                breakdown.Items.Add(new BreakdownItemDTO { Label = "Base IDV Settlement", Value = insuredIdv });
                breakdown.FinalPayout = Math.Round(insuredIdv, 2);
            }
            else if (claim.claimType == ClaimType.Damage)
            {
                var repair = dto.RepairCost ?? 0m;
                var engine = (plan.EngineProtectionAvailable && dto.EngineCost != null) ? dto.EngineCost.Value : 0m;

                decimal depPercent = _pricingService.GetDepreciationRate(age);

                bool isZeroDep = plan.ZeroDepreciationAvailable;
                decimal effectiveDep = isZeroDep ? 0m : depPercent;

                decimal depRepair = repair * effectiveDep;
                decimal depEngine = engine * effectiveDep;

                breakdown.Items.Add(new BreakdownItemDTO { Label = "Authorized Repair Cost", Value = repair });
                if (engine > 0) breakdown.Items.Add(new BreakdownItemDTO { Label = "Engine Protection Cover", Value = engine });

                if (effectiveDep > 0)
                {
                    breakdown.Items.Add(new BreakdownItemDTO { Label = $"Depreciation Applied ({(effectiveDep * 100):0}%)", Value = -(depRepair + depEngine), Status = "error" });
                }
                else if (isZeroDep && (repair + engine) > 0)
                {
                    breakdown.Items.Add(new BreakdownItemDTO { Label = "Zero Depreciation Bonus", Value = 0, Status = "success", Note = "Saved by plan" });
                }

                decimal subtotal = (repair + engine) - (depRepair + depEngine);

                if (subtotal >= insuredIdv * 0.75m)
                {
                    breakdown.IsTotalLoss = true;
                    breakdown.Items.Clear();
                    breakdown.Items.Add(new BreakdownItemDTO { Label = "Calculated Damage Cost", Value = subtotal });
                    breakdown.Items.Add(new BreakdownItemDTO { Label = "Total Loss Adjustment", Value = insuredIdv - subtotal, Status = "warning", Note = "Damage > 75% IDV" });
                    subtotal = insuredIdv;
                }

                breakdown.Items.Add(new BreakdownItemDTO { Label = "Compulsory Deductible", Value = -breakdown.Deductible, Status = "error" });

                decimal final = subtotal - breakdown.Deductible;
                if (breakdown.MaxCoverage > 0 && final > breakdown.MaxCoverage)
                {
                    breakdown.IsCapped = true;
                    breakdown.Items.Add(new BreakdownItemDTO { Label = "Max Coverage Cap", Value = -(final - breakdown.MaxCoverage), Status = "error", Note = "Plan Limit" });
                    final = breakdown.MaxCoverage;
                }

                breakdown.FinalPayout = Math.Max(0, Math.Round(final, 2));
            }
            else if (claim.claimType == ClaimType.ThirdParty)
            {
                var repair = dto.RepairCost ?? 0m;
                if (dto.ManufactureYear == null) throw new BadRequestException("Manufacture year required for third party");

                int tpAge = currentYear - dto.ManufactureYear.Value;
                decimal depP = _pricingService.GetDepreciationRate(tpAge);
                
                bool isZeroDep = plan.ZeroDepreciationAvailable;
                decimal effectiveDep = isZeroDep ? 0m : depP;

                decimal depAmt = repair * effectiveDep;
                breakdown.Items.Add(new BreakdownItemDTO { Label = "3rd Party Repair Bill", Value = repair });

                if (effectiveDep > 0)
                {
                    breakdown.Items.Add(new BreakdownItemDTO { Label = $"Age Depreciation ({(effectiveDep * 100):0}%)", Value = -depAmt, Status = "error" });
                }
                else if (isZeroDep && repair > 0)
                {
                    breakdown.Items.Add(new BreakdownItemDTO { Label = "Zero Depreciation Bonus", Value = 0, Status = "success", Note = "Saved by plan" });
                }
                breakdown.Items.Add(new BreakdownItemDTO { Label = "Compulsory Deductible", Value = -breakdown.Deductible, Status = "error" });

                decimal final = repair - depAmt - breakdown.Deductible;
                if (breakdown.MaxCoverage > 0 && final > breakdown.MaxCoverage)
                {
                    breakdown.IsCapped = true;
                    breakdown.Items.Add(new BreakdownItemDTO { Label = "Max Coverage Cap", Value = -(final - breakdown.MaxCoverage), Status = "error" });
                    final = breakdown.MaxCoverage;
                }
                breakdown.FinalPayout = Math.Max(0, Math.Round(final, 2));
            }

            return breakdown;
        }

    }
}

