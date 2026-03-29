using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;

namespace VIMS.Application.Services.AdminAI
{
    public class BusinessRuleEvaluatorService : IBusinessRuleEvaluatorService
    {
        public BusinessRuleAnalysisDto Evaluate(
            IntentResultDto intent,
            IReadOnlyList<ClaimContextDto> claims,
            IReadOnlyList<PolicyContextDto> policies,
            IReadOnlyList<PaymentContextDto> payments,
            PaymentAggregateContextDto? paymentAggregates)
        {
            var analysis = new BusinessRuleAnalysisDto
            {
                Eligibility = true
            };

            var now = DateTime.UtcNow;

            var policiesById = policies
                .GroupBy(p => p.PolicyId)
                .ToDictionary(g => g.Key, g => g.First());

            var expiredPolicies = policies
                .Where(p => IsExpiredPolicy(p, now))
                .ToList();

            foreach (var policy in expiredPolicies)
            {
                analysis.Violations.Add(new BusinessRuleViolationDto
                {
                    Code = "POLICY_EXPIRED",
                    Message = $"Policy {policy.PolicyNumber} is expired and not eligible for claim/processing.",
                    EntityType = "Policy",
                    EntityId = policy.PolicyId.ToString()
                });
            }

            var pendingPaymentPolicies = policies
                .Where(p => IsPendingPaymentPolicy(p.Status))
                .ToList();

            foreach (var policy in pendingPaymentPolicies)
            {
                analysis.Violations.Add(new BusinessRuleViolationDto
                {
                    Code = "PAYMENT_PENDING",
                    Message = $"Policy {policy.PolicyNumber} has pending premium payment and is not eligible.",
                    EntityType = "Policy",
                    EntityId = policy.PolicyId.ToString()
                });
            }

            var claimsExceedingIdv = new List<(ClaimContextDto Claim, PolicyContextDto Policy)>();
            foreach (var claim in claims)
            {
                if (!claim.ApprovedAmount.HasValue)
                {
                    continue;
                }

                if (!policiesById.TryGetValue(claim.PolicyId, out var policy))
                {
                    continue;
                }

                if (policy.IDV <= 0)
                {
                    continue;
                }

                if (claim.ApprovedAmount.Value > policy.IDV)
                {
                    claimsExceedingIdv.Add((claim, policy));
                    analysis.Violations.Add(new BusinessRuleViolationDto
                    {
                        Code = "CLAIM_EXCEEDS_IDV",
                        Message = $"Claim {claim.ClaimNumber} amount {claim.ApprovedAmount.Value:0.##} exceeds policy IDV {policy.IDV:0.##}.",
                        EntityType = "Claim",
                        EntityId = claim.ClaimId.ToString()
                    });
                }
            }

            if (analysis.Violations.Any(v =>
                    v.Code == "POLICY_EXPIRED" ||
                    v.Code == "PAYMENT_PENDING" ||
                    v.Code == "CLAIM_EXCEEDS_IDV"))
            {
                analysis.Eligibility = false;
            }

            var maxClaimToIdvRatio = claimsExceedingIdv.Count == 0
                ? 0m
                : claimsExceedingIdv.Max(x => x.Policy.IDV == 0m ? 0m : x.Claim.ApprovedAmount!.Value / x.Policy.IDV);

            analysis.ComputedValues["intentType"] = intent.IntentType;
            analysis.ComputedValues["claimsReviewed"] = claims.Count;
            analysis.ComputedValues["policiesReviewed"] = policies.Count;
            analysis.ComputedValues["paymentsReviewed"] = payments.Count;
            analysis.ComputedValues["violationsCount"] = analysis.Violations.Count;
            analysis.ComputedValues["expiredPoliciesCount"] = expiredPolicies.Count;
            analysis.ComputedValues["pendingPaymentPoliciesCount"] = pendingPaymentPolicies.Count;
            analysis.ComputedValues["claimsExceedingIdvCount"] = claimsExceedingIdv.Count;
            analysis.ComputedValues["maxClaimToIdvRatio"] = decimal.Round(maxClaimToIdvRatio, 4);
            analysis.ComputedValues["totalPaidAmount"] = paymentAggregates?.TotalPaidAmount ?? 0m;
            analysis.ComputedValues["totalAmountAllStatuses"] = paymentAggregates?.TotalAmountAllStatuses ?? 0m;
            analysis.ComputedValues["paidPaymentsCount"] = paymentAggregates?.PaidPaymentsCount ?? 0;
            analysis.ComputedValues["totalPaymentsCount"] = paymentAggregates?.TotalPaymentsCount ?? 0;

            return analysis;
        }

        private static bool IsPendingPaymentPolicy(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var normalized = status.Trim().Replace(" ", string.Empty).ToLowerInvariant();
            return normalized == "pendingpayment" || normalized == "1";
        }

        private static bool IsExpiredPolicy(PolicyContextDto policy, DateTime utcNow)
        {
            if (policy.EndDate < utcNow)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(policy.Status))
            {
                return false;
            }

            var normalized = policy.Status.Trim().Replace(" ", string.Empty).ToLowerInvariant();
            return normalized == "expired" || normalized == "4";
        }
    }
}