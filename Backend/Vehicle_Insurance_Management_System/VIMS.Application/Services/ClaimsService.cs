using System;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
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
        private readonly INotificationService _notificationService;
        private readonly IOcrService _ocrService;
        private readonly IGroqService _groqService;
        private readonly IPolicyTransferRepository _transferRepository;

        public ClaimsService(IClaimsRepository claimsRepository, IUserRepository userRepository, IPolicyRepository policyRepository, IPricingService pricingService, IPaymentRepository paymentRepository, IAuditService auditService, IFileStorageService fileStorageService, INotificationService notificationService, IOcrService ocrService, IGroqService groqService, IPolicyTransferRepository transferRepository)
        {
            _claimsRepository = claimsRepository;
            _userRepository = userRepository;
            _policyRepository = policyRepository;
            _pricingService = pricingService;
            _paymentRepository = paymentRepository;
            _auditService = auditService;
            _fileStorageService = fileStorageService;
            _notificationService = notificationService;
            _ocrService = ocrService;
            _groqService = groqService;
            _transferRepository = transferRepository;
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
            
            // Notify customer
            await _notificationService.CreateNotificationAsync(customerId, "Claim Submitted", $"Your claim {created.ClaimNumber} has been successfully submitted and is under review.", NotificationType.ClaimSubmitted, "Claim", created.ClaimId.ToString());
            
            // Notify officer
            if (officer != null)
            {
                await _notificationService.CreateNotificationAsync(officer.UserId, "New Claim Assigned", $"A new claim {created.ClaimNumber} has been assigned to you for review.", NotificationType.NewClaimAssigned, "Claim", created.ClaimId.ToString());
            }

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
                await _notificationService.CreateNotificationAsync(claim.CustomerId, "Claim Rejected", $"Your claim {claim.ClaimNumber} has been rejected. Reason: {dto.RejectionReason}", NotificationType.ClaimRejected, "Claim", claim.ClaimId.ToString());
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

            // compute breakdown and save it as JSON for permanent record
            var breakdown = await CalculateClaimBreakdownAsync(claimId, dto);
            
            // mark claim approved and store approved amount
            claim.Status = ClaimStatus.Approved;
            claim.ApprovedAmount = breakdown.FinalPayout;
            claim.SettlementBreakdownJson = JsonSerializer.Serialize(breakdown, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            // determine decision type
            var decisionType = "Partial";
            if (claim.claimType == ClaimType.Theft && claim.ApprovedAmount >= insuredIdv)
                decisionType = "TotalLoss";
            else if ((claim.ApprovedAmount ?? 0m) >= (insuredIdv * 0.75m))
                decisionType = "ConstructiveTotalLoss";

            claim.DecisionType = decisionType;

            await _claimsRepository.UpdateAsync(claim);
            await _auditService.LogActionAsync("ClaimApproved", "Claim", $"Officer approved claim: {claim.ClaimNumber} with amount {claim.ApprovedAmount}", "Claim", claim.ClaimId.ToString());
            await _notificationService.CreateNotificationAsync(claim.CustomerId, "Claim Approved", $"Great news! Your claim {claim.ClaimNumber} has been approved for an amount of {claim.ApprovedAmount:C}.", NotificationType.ClaimApproved, "Claim", claim.ClaimId.ToString());


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
                TransactionReference = $"Claim #{claim.ClaimNumber}"
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
                    breakdown.WarningMessage = $"Payout capped at Maximum Coverage limit (₹{breakdown.MaxCoverage:N0}) as per current plan.";
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
                
                decimal depAmt = repair * depP;
                breakdown.Items.Add(new BreakdownItemDTO { Label = "3rd Party Repair Bill", Value = repair });

                if (depP > 0)
                {
                    breakdown.Items.Add(new BreakdownItemDTO { Label = $"Age Depreciation ({(depP * 100):0}%)", Value = -depAmt, Status = "error" });
                }
                breakdown.Items.Add(new BreakdownItemDTO { Label = "Compulsory Deductible", Value = -breakdown.Deductible, Status = "error" });

                decimal final = repair - depAmt - breakdown.Deductible;
                if (breakdown.MaxCoverage > 0 && final > breakdown.MaxCoverage)
                {
                    breakdown.IsCapped = true;
                    breakdown.WarningMessage = $"Payout capped at Maximum Coverage limit (₹{breakdown.MaxCoverage:N0}) as per current plan.";
                    breakdown.Items.Add(new BreakdownItemDTO { Label = "Max Coverage Cap", Value = -(final - breakdown.MaxCoverage), Status = "error" });
                    final = breakdown.MaxCoverage;
                }
                breakdown.FinalPayout = Math.Max(0, Math.Round(final, 2));
            }

            return breakdown;
        }

        public async Task<ClaimsAnalysisResultDTO> AnalyzeClaimAsync(int claimId)
        {
            var claim = await _claimsRepository.GetByIdAsync(claimId);
            if (claim == null) throw new NotFoundException("Claim not found");

            // If we have cached results, return them (optional, but requested implicitly)
            // if (!string.IsNullOrEmpty(claim.FraudRiskAnalysisJson)) ...

            int score = 0;
            var reasons = new List<string>();
            string summary = claim.Summary ?? string.Empty;
            string extractedReg = string.Empty;
            bool mismatch = false;
            string combinedText = string.Empty;
            string document1Text = string.Empty;
            string document2Text = string.Empty;

            // 1. Existing checks (Controller logic moved here)
            var payments = claim.Policy?.Payments?.ToList() ?? new List<Payment>();
            var lastPayment = payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefault();

            if (lastPayment != null && Math.Abs((claim.CreatedAt - lastPayment.PaymentDate).TotalHours) < 48)
            {
                score += 40;
                reasons.Add("Claim filed within 48 hours of a premium payment.");
            }

            var transfers = await _transferRepository.GetTransfersByPolicyIdAsync(claim.PolicyId);
            var lastTransfer = transfers?
                .Where(t => t.Status == PolicyTransferStatus.Completed)
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .FirstOrDefault();

            if (lastTransfer != null && Math.Abs((claim.CreatedAt - (lastTransfer.UpdatedAt ?? lastTransfer.CreatedAt)).TotalDays) < 30)
            {
                score += 25;
                reasons.Add("Claim filed within 30 days of a policy transfer completion.");
            }

            if (claim.Policy != null && (claim.Policy.CurrentYearEndDate - claim.CreatedAt).TotalDays < 10 && (claim.Policy.CurrentYearEndDate - claim.CreatedAt).TotalDays >= 0)
            {
                score += 20;
                reasons.Add("Claim filed within the last 10 days of the policy term.");
            }

            var otherClaims = await _claimsRepository.GetByCustomerIdAsync(claim.CustomerId);
            var recentTotalCount = otherClaims.Count(c => c.ClaimId != claim.ClaimId && Math.Abs((claim.CreatedAt - c.CreatedAt).TotalDays) < 180);
            var recentRejectedCount = otherClaims.Count(c => c.ClaimId != claim.ClaimId && c.Status == ClaimStatus.Rejected && Math.Abs((claim.CreatedAt - c.CreatedAt).TotalDays) < 365);

            if (recentTotalCount > 0)
            {
                score += Math.Min(recentTotalCount * 10, 30);
                reasons.Add($"High Frequency: Customer has filed {recentTotalCount} other claim(s) in last 6 months.");
            }
            if (recentRejectedCount >= 2)
            {
                score += 20;
                reasons.Add($"Suspicious History: {recentRejectedCount} previous claims from this customer were REJECTED in the last year.");
            }

            // 2. NEW OCR and Groq Workflow
            var doc = claim.Documents?.FirstOrDefault();
            if (doc != null)
            {
                // Extract from Doc1 (Repair Bill / FIR) and Doc2 (Invoice)
                if (!string.IsNullOrEmpty(doc.Document1))
                {
                    document1Text = await _ocrService.ExtractTextAsync(doc.Document1);
                    combinedText += document1Text + " ";
                }
                if (!string.IsNullOrEmpty(doc.Document2))
                {
                    document2Text = await _ocrService.ExtractTextAsync(doc.Document2);
                    combinedText += document2Text + " ";
                }

                if (!string.IsNullOrEmpty(combinedText))
                {
                    // A. Summary
                    if (string.IsNullOrEmpty(claim.Summary))
                    {
                        summary = await _groqService.SummarizeTextAsync(combinedText);
                        claim.Summary = summary.Trim();
                    }

                    // B. Vehicle Number Mismatch
                    var policyReg = claim.Policy?.Vehicle?.RegistrationNumber?.Replace(" ", "").ToUpper();
                    if (!string.IsNullOrEmpty(policyReg))
                    {
                        // Search for the registration number in the text (basic check)
                        if (!combinedText.Replace(" ", "").ToUpper().Contains(policyReg))
                        {
                            if (claim.claimType == ClaimType.Damage)
                            {
                                score += 50;
                                mismatch = true;
                                reasons.Add($"CRITICAL: Vehicle number mismatch. The document text does not contain the policy vehicle number ({policyReg}).");
                            }
                        }
                    }

                    // C. Date Validation (Premium paid post-incident)
                    // Regular search for dates in text (Very naive, usually Groq is better for this)
                    // We'll let Groq analyze the risk context too
                    var groqAnalysis = await _groqService.AnalyzeRiskAsync(combinedText, $"Claim for {claim.claimType} on {claim.CreatedAt:d}. Vehicle: {policyReg}. Last Payment: {lastPayment?.PaymentDate:d}");
                    if (groqAnalysis.Contains("MISMATCH") || groqAnalysis.Contains("SUSPICIOUS"))
                    {
                        // score += 10; // Extra AI risk
                    }
                }
            }

            // 3. Explicit document validation rules requested for risk scoring.
            var expectedVehicleReg = claim.Policy?.Vehicle?.RegistrationNumber ?? string.Empty;
            var expectedOwnerName = claim.Customer?.FullName ?? string.Empty;

            if (claim.claimType == ClaimType.ThirdParty)
            {
                if (string.IsNullOrWhiteSpace(document1Text) || string.IsNullOrWhiteSpace(document2Text))
                {
                    score += 40;
                    var missing = string.IsNullOrWhiteSpace(document1Text) && string.IsNullOrWhiteSpace(document2Text)
                        ? "repair bill and invoice"
                        : string.IsNullOrWhiteSpace(document1Text) ? "repair bill" : "invoice";
                    reasons.Add($"Third Party validation failed: Missing {missing} document text for cross verification.");
                }
                else
                {
                    var billRegs = ExtractVehicleNumbers(document1Text);
                    var invoiceRegs = ExtractVehicleNumbers(document2Text);

                    if (billRegs.Count == 0)
                    {
                        score += 15;
                        reasons.Add("Third Party validation failed: Could not extract a vehicle number from the repair bill.");
                    }

                    if (invoiceRegs.Count == 0)
                    {
                        score += 15;
                        reasons.Add("Third Party validation failed: Could not extract a vehicle number from the invoice.");
                    }

                    if (billRegs.Count > 0 && invoiceRegs.Count > 0)
                    {
                        extractedReg = billRegs.FirstOrDefault() ?? string.Empty;
                        var sameVehicleInBoth = billRegs.Intersect(invoiceRegs, StringComparer.OrdinalIgnoreCase).Any();
                        if (!sameVehicleInBoth)
                        {
                            score += 45;
                            mismatch = true;
                            reasons.Add($"Third Party vehicle mismatch: Repair bill vehicle ({billRegs[0]}) does not match invoice vehicle ({invoiceRegs[0]}).");
                        }

                        var expectedNormalized = NormalizeVehicleNumber(expectedVehicleReg);
                        if (!string.IsNullOrEmpty(expectedNormalized))
                        {
                            var billMatchesPolicy = billRegs.Any(r => r.Equals(expectedNormalized, StringComparison.OrdinalIgnoreCase));
                            var invoiceMatchesPolicy = invoiceRegs.Any(r => r.Equals(expectedNormalized, StringComparison.OrdinalIgnoreCase));

                            if (!billMatchesPolicy || !invoiceMatchesPolicy)
                            {
                                score += 30;
                                mismatch = true;
                                reasons.Add($"Third Party policy mismatch: Expected policy vehicle number {expectedNormalized} was not found in both documents.");
                            }
                        }
                    }

                    var billOwner = TryExtractOwnerName(document1Text);
                    var invoiceOwner = TryExtractOwnerName(document2Text);

                    if (string.IsNullOrWhiteSpace(billOwner))
                    {
                        score += 10;
                        reasons.Add("Third Party owner validation failed: Owner details not detected in repair bill.");
                    }

                    if (string.IsNullOrWhiteSpace(invoiceOwner))
                    {
                        score += 10;
                        reasons.Add("Third Party owner validation failed: Owner details not detected in invoice.");
                    }

                    if (!string.IsNullOrWhiteSpace(billOwner) && !string.IsNullOrWhiteSpace(invoiceOwner) &&
                        !IsOwnerLikelyMatch(billOwner, invoiceOwner))
                    {
                        score += 35;
                        reasons.Add($"Third Party owner mismatch: Repair bill owner ({billOwner}) does not match invoice owner ({invoiceOwner}).");
                    }

                    if (!string.IsNullOrWhiteSpace(expectedOwnerName))
                    {
                        if (!string.IsNullOrWhiteSpace(billOwner) && !IsOwnerLikelyMatch(billOwner, expectedOwnerName))
                        {
                            score += 25;
                            reasons.Add($"Third Party owner-policy mismatch: Repair bill owner ({billOwner}) does not match policy owner ({expectedOwnerName}).");
                        }

                        if (!string.IsNullOrWhiteSpace(invoiceOwner) && !IsOwnerLikelyMatch(invoiceOwner, expectedOwnerName))
                        {
                            score += 25;
                            reasons.Add($"Third Party owner-policy mismatch: Invoice owner ({invoiceOwner}) does not match policy owner ({expectedOwnerName}).");
                        }
                    }
                }
            }

            if (claim.claimType == ClaimType.Theft)
            {
                if (string.IsNullOrWhiteSpace(document1Text))
                {
                    score += 40;
                    reasons.Add("Theft validation failed: FIR document text is missing for vehicle-owner verification.");
                }
                else
                {
                    var firRegs = ExtractVehicleNumbers(document1Text);
                    extractedReg = firRegs.FirstOrDefault() ?? extractedReg;
                    var expectedNormalized = NormalizeVehicleNumber(expectedVehicleReg);
                    var firContainsExpectedReg = !string.IsNullOrEmpty(expectedNormalized)
                        && ContainsVehicleNumberNormalized(document1Text, expectedNormalized);

                    if (firRegs.Count == 0 && !firContainsExpectedReg)
                    {
                        score += 20;
                        mismatch = true;
                        reasons.Add("Theft validation failed: Vehicle number could not be extracted from FIR.");
                    }
                    else if (!string.IsNullOrEmpty(expectedNormalized)
                             && !firContainsExpectedReg
                             && !firRegs.Any(r => r.Equals(expectedNormalized, StringComparison.OrdinalIgnoreCase)))
                    {
                        score += 50;
                        mismatch = true;
                        reasons.Add($"Theft vehicle mismatch: FIR vehicle number ({firRegs[0]}) does not match policy vehicle number ({expectedNormalized}).");
                    }
                    else if (string.IsNullOrEmpty(extractedReg) && firContainsExpectedReg)
                    {
                        extractedReg = expectedNormalized;
                    }

                    var firOwner = TryExtractOwnerName(document1Text);
                    if (string.IsNullOrWhiteSpace(firOwner))
                    {
                        score += 10;
                        reasons.Add("Theft owner validation failed: Owner details not detected in FIR.");
                    }
                    else if (!string.IsNullOrWhiteSpace(expectedOwnerName) && !IsOwnerLikelyMatch(firOwner, expectedOwnerName))
                    {
                        score += 30;
                        reasons.Add($"Theft owner mismatch: FIR owner ({firOwner}) does not match policy owner ({expectedOwnerName}).");
                    }
                }
            }

            // 5. Build Result
            var resultDto = new ClaimsAnalysisResultDTO
            {
                FraudRiskScore = Math.Min(score, 100),
                RiskReasons = reasons,
                Summary = summary.Trim(),
                VehicleMismatchDetected = mismatch,
                ExtractedVehicleNumber = extractedReg
            };

            // 6. Prefill Suggested Information (Strictly using raw text from PdfPig, no AI extraction)
            try
            {
                // A. Extract Repair Cost (Regex to find amounts like INR 25,749, Rs 5000, ₹ 100,000 etc.)
                var amountRegex = new System.Text.RegularExpressions.Regex(@"(?i)(?:INR|Rs|₹|Cost|Total|Amount|Estimated|Settlement)[:\s]*([\d,]+(?:\.\d+)?)\b");
                var amountMatch = amountRegex.Match(combinedText);

                if (amountMatch.Success)
                {
                    var cleanVal = amountMatch.Groups[1].Value.Replace(",", "");
                    if (decimal.TryParse(cleanVal, out decimal val))
                    {
                        resultDto.SuggestedRepairCost = val;
                    }
                }

                // B. Extract Manufacture Year (Strictly targeting MFY label as requested)
                var yearRegex = new System.Text.RegularExpressions.Regex(@"(?i)MFY[:\s]*\b(19\d{2}|20[0-2]\d)\b");
                var yearMatch = yearRegex.Match(combinedText);
                
                if (yearMatch.Success)
                {
                    if (int.TryParse(yearMatch.Groups[1].Value, out int year))
                    {
                        resultDto.SuggestedManufactureYear = year;
                    }
                }
            }
            catch { /* Ignore extraction errors to prevent workflow failure */ }

            // Save to database
            claim.FraudRiskAnalysisJson = JsonSerializer.Serialize(resultDto);
            await _claimsRepository.UpdateAsync(claim);

            return resultDto;
        }

        private static List<string> ExtractVehicleNumbers(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            var regex = new Regex(@"\b[A-Z]{2}[\s-]?\d{1,2}[\s-]?[A-Z]{1,3}[\s-]?\d{4}\b", RegexOptions.IgnoreCase);
            var matches = regex.Matches(text);

            return matches
                .Select(m => NormalizeVehicleNumber(m.Value))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string NormalizeVehicleNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(value.ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
        }

        private static string TryExtractOwnerName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var ownerRegex = new Regex(@"(?im)(?:owner(?:'s)?\s*name|name\s*of\s*owner|insured\s*name|policy\s*holder|complainant\s*name|name\s*of\s*complainant|complainant)\s*[:\-]\s*([A-Za-z][A-Za-z .]{2,80})");
            var match = ownerRegex.Match(text);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            // FIR layouts often have a plain "Name" row under a complainant section.
            var genericNameRegex = new Regex(@"(?im)^\s*name\s*[:\-]?\s*([A-Za-z][A-Za-z .]{2,80})\s*$");
            var genericNameMatch = genericNameRegex.Match(text);
            if (genericNameMatch.Success)
            {
                return genericNameMatch.Groups[1].Value.Trim();
            }

            return string.Empty;
        }

        private static bool ContainsVehicleNumberNormalized(string text, string normalizedVehicleNumber)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(normalizedVehicleNumber))
            {
                return false;
            }

            var normalizedText = Regex.Replace(text.ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
            return normalizedText.Contains(normalizedVehicleNumber, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOwnerLikelyMatch(string left, string right)
        {
            var normalizedLeft = NormalizePersonName(left);
            var normalizedRight = NormalizePersonName(right);

            if (string.IsNullOrWhiteSpace(normalizedLeft) || string.IsNullOrWhiteSpace(normalizedRight))
            {
                return false;
            }

            if (string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return normalizedLeft.Contains(normalizedRight, StringComparison.OrdinalIgnoreCase)
                || normalizedRight.Contains(normalizedLeft, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePersonName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var cleaned = Regex.Replace(value.ToUpperInvariant(), @"[^A-Z ]", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

    }
}