using System.Text.RegularExpressions;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;

namespace VIMS.Application.Services.AdminAI
{
    public class IntentParser : IIntentParser
    {
        private static readonly Regex ClaimIdRegex = new(@"\bclaim(?:\s*|[-_])?(?:id|number|no|#)?\s*[:#=\-]?\s*(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UserIdRegex = new(@"\b(?:user|customer)(?:\s*|[-_])?(?:id|number|no|#)?\s*[:#=\-]?\s*(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PolicyIdRegex = new(@"\bpolicy(?:\s*|[-_])?(?:id|number|no|#)?\s*[:#=\-]?\s*(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DaysRangeRegex = new(@"\b(\d{1,3})\s*days?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PronounReferenceRegex = new(@"\b(this|that|it|this\s+one|that\s+one)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IntentResultDto Parse(string question, IReadOnlyList<string>? history = null, ContextMemoryDto? sessionMemory = null)
        {
            var normalized = (question ?? string.Empty).Trim();
            var lower = normalized.ToLowerInvariant();
            var hasExplicitIntent = IsExplicitIntentQuery(lower);
            var historyMemory = BuildContextMemory(history);
            var contextMemory = hasExplicitIntent
                ? new ContextMemoryDto()
                : MergeMemory(sessionMemory, historyMemory);
            var hasPronounReference = PronounReferenceRegex.IsMatch(normalized);
            var isRegistrationDetailFollowUp = ContainsAny(lower, "registration number", "registration no", "registration details", "give me registration");
            var requestedRangeDays = TryExtractRangeDays(normalized);
            var requiresExplanation = ContainsAny(lower, "why", "explain", "reason", "rule", "eligibility", "violation");
            var vehicleTypeFilter = ExtractVehicleTypeFilter(lower);
            var fuelTypeFilter = ExtractFuelTypeFilter(lower);
            var planTypeFilter = ExtractPlanTypeFilter(lower);

            var includeClaims = ContainsAny(lower, "claim", "rejected", "rejection", "approved", "fraud");
            var includeUsers = ContainsAny(lower, "user", "customer", "officer", "admin");
            var includePolicies = ContainsAny(lower, "policy", "eligible", "coverage", "plan", "premium", "pending premium", "pending payment", "unpaid premium", "premium due", "overdue premium", "idv");
            var includeReferrals = ContainsAny(lower, "referral", "refer", "abuse", "referrer", "referee", "code");
            var includeVehicles = ContainsAny(lower, "vehicle", "registration", "car", "idv");
            var includePayments = ContainsAny(lower, "payment", "premium", "invoice", "transaction", "collected");
            var includeApplications = ContainsAny(lower, "application", "applied", "under review", "transfer");
            var includeGarages = ContainsAny(lower, "garage", "workshop", "service center", "service centre");
            var includeNotifications = ContainsAny(lower, "notification", "notifications", "alert", "message");

            var claimId = TryExtractId(ClaimIdRegex, normalized);
            var userId = TryExtractId(UserIdRegex, normalized);
            var policyId = TryExtractId(PolicyIdRegex, normalized);

            if ((hasPronounReference || isRegistrationDetailFollowUp) && !policyId.HasValue && contextMemory.AnchorPolicyId.HasValue)
            {
                policyId = contextMemory.AnchorPolicyId.Value;
                includePolicies = true;
                includeVehicles = true;
            }

            if (hasPronounReference)
            {
                var referencedType = ResolveReferencedEntityType(lower, contextMemory.EntityType);
                if (TryParseContextEntityId(contextMemory.LastEntityId, out var contextEntityId))
                {
                    switch (referencedType)
                    {
                        case "claim" when !claimId.HasValue && string.Equals(contextMemory.EntityType, "claim", StringComparison.OrdinalIgnoreCase):
                            claimId = contextEntityId;
                            includeClaims = true;
                            break;
                        case "policy" when !policyId.HasValue && string.Equals(contextMemory.EntityType, "policy", StringComparison.OrdinalIgnoreCase):
                            policyId = contextEntityId;
                            includePolicies = true;
                            break;
                        case "user" when !userId.HasValue && string.Equals(contextMemory.EntityType, "user", StringComparison.OrdinalIgnoreCase):
                            userId = contextEntityId;
                            includeUsers = true;
                            break;
                    }
                }

                if (!includeClaims && !includeUsers && !includePolicies && !includeReferrals && !includeVehicles && !includePayments && !includeApplications && !includeGarages && !includeNotifications)
                {
                    ApplyIntentIncludes(contextMemory.LastIntent, ref includeClaims, ref includeUsers, ref includePolicies, ref includeReferrals, ref includeVehicles, ref includePayments, ref includeApplications, ref includeGarages, ref includeNotifications);
                }

                if (string.Equals(contextMemory.EntityType, "policy", StringComparison.OrdinalIgnoreCase) && !policyId.HasValue && contextMemory.AnchorPolicyId.HasValue)
                {
                    policyId = contextMemory.AnchorPolicyId.Value;
                    includePolicies = true;
                }
            }

            if (!includeClaims && !includeUsers && !includePolicies && !includeReferrals && !includeVehicles && !includePayments && !includeApplications && !includeGarages && !includeNotifications)
            {
                includeClaims = true;
                includeUsers = true;
                includePolicies = true;
                includeReferrals = true;
                includeVehicles = true;
                includePayments = true;
                includeApplications = true;
                includeGarages = true;
                includeNotifications = true;
            }

            var result = new IntentResultDto
            {
                IncludeClaims = includeClaims,
                IncludeUsers = includeUsers,
                IncludePolicies = includePolicies,
                IncludeReferrals = includeReferrals,
                IncludeVehicles = includeVehicles,
                IncludePayments = includePayments,
                IncludeApplications = includeApplications,
                IncludeGarages = includeGarages,
                IncludeNotifications = includeNotifications,
                ClaimId = claimId,
                UserId = userId,
                PolicyId = policyId,
                RequestedRangeDays = requestedRangeDays,
                VehicleTypeFilter = vehicleTypeFilter,
                FuelTypeFilter = fuelTypeFilter,
                PlanTypeFilter = planTypeFilter,
                RequiresExplanation = requiresExplanation,
                IsFollowUp = hasPronounReference || isRegistrationDetailFollowUp
            };

            result.IntentType = ResolveIntentType(result);
            result.QueryPlanHint = ResolveQueryPlanHint(lower, result);
            result.ContextMemory = BuildCurrentContextMemory(result, contextMemory);
            return result;
        }

        private static bool IsExplicitIntentQuery(string lower)
        {
            return ContainsAny(
                lower,
                "which vehicles",
                "what all vehicles",
                "list",
                "all policies",
                "show all",
                "expiring",
                "expiry",
                "total premium",
                "pending payment",
                "pending premium",
                "highest idv",
                "zero depreciation",
                "ev",
                "car",
                "two-wheeler",
                "bike",
                "plan");
        }

        private static string ExtractVehicleTypeFilter(string lower)
        {
            if (ContainsAny(lower, "two-wheeler", "2 wheeler", "bike", "motorcycle", "scooter")) return "bike";
            if (ContainsAny(lower, "car", "four-wheeler", "4 wheeler", "sedan", "hatchback", "suv")) return "car";
            return string.Empty;
        }

        private static string ExtractFuelTypeFilter(string lower)
        {
            if (ContainsAny(lower, "ev", "electric")) return "electric";
            if (ContainsAny(lower, "diesel")) return "diesel";
            if (ContainsAny(lower, "petrol", "gasoline")) return "petrol";
            if (ContainsAny(lower, "cng")) return "cng";
            return string.Empty;
        }

        private static string ExtractPlanTypeFilter(string lower)
        {
            if (ContainsAny(lower, "zero depreciation", "zero dep")) return "zerodepreciation";
            if (ContainsAny(lower, "comprehensive")) return "comprehensive";
            if (ContainsAny(lower, "third party")) return "thirdparty";
            return string.Empty;
        }

        private static int? TryExtractRangeDays(string input)
        {
            var match = DaysRangeRegex.Match(input);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed) && parsed > 0)
            {
                return parsed;
            }

            var lower = input.ToLowerInvariant();
            if (ContainsAny(lower, "next week", "this week"))
            {
                return 7;
            }

            if (ContainsAny(lower, "next month", "this month"))
            {
                return 30;
            }

            return null;
        }

        private static ContextMemoryDto MergeMemory(ContextMemoryDto? persisted, ContextMemoryDto fromHistory)
        {
            if (persisted == null)
            {
                return fromHistory;
            }

            return new ContextMemoryDto
            {
                LastIntent = string.IsNullOrWhiteSpace(fromHistory.LastIntent) || fromHistory.LastIntent == "GENERAL" ? persisted.LastIntent : fromHistory.LastIntent,
                LastEntityId = string.IsNullOrWhiteSpace(fromHistory.LastEntityId) ? persisted.LastEntityId : fromHistory.LastEntityId,
                EntityType = string.IsNullOrWhiteSpace(fromHistory.EntityType) ? persisted.EntityType : fromHistory.EntityType,
                AnchorPolicyId = fromHistory.AnchorPolicyId ?? persisted.AnchorPolicyId,
                AnchorVehicleId = fromHistory.AnchorVehicleId ?? persisted.AnchorVehicleId,
                LastPlanType = string.IsNullOrWhiteSpace(fromHistory.LastPlanType) || fromHistory.LastPlanType == "NONE" ? persisted.LastPlanType : fromHistory.LastPlanType,
                LastRangeDays = fromHistory.LastRangeDays ?? persisted.LastRangeDays,
                LastUpdatedUtc = persisted.LastUpdatedUtc
            };
        }

        private static string ResolveQueryPlanHint(string lower, IntentResultDto intent)
        {
            if (ContainsAny(lower, "rejected", "rejection") && ContainsAny(lower, "today")) return "REJECTED_CLAIMS_TODAY";
            if (ContainsAny(lower, "expiring", "expiry", "expires", "due for renewal")) return "EXPIRING_POLICIES";
            if (ContainsAny(lower, "total premium", "sum premium", "total premiums", "premium collected"))
            {
                if (ContainsAny(lower, "collected", "paid")) return "TOTAL_PREMIUM_COLLECTED";
                if (ContainsAny(lower, "pending", "unpaid")) return "TOTAL_PREMIUM_PENDING";
                return "TOTAL_PREMIUM";
            }
            if (ContainsAny(lower, "highest idv", "max idv", "maximum idv", "top idv")) return "HIGHEST_IDV";
            if (ContainsAny(lower, "pending payment", "pending premium", "unpaid premium", "premium due", "overdue premium")) return "PENDING_PAYMENT_POLICIES";
            if (ContainsAny(lower, "zero depreciation")) return "ZERO_DEPRECIATION_POLICIES";
            if (ContainsAny(lower, "registration number", "registration no", "vehicle registration") && (intent.PolicyId.HasValue || intent.ContextMemory.AnchorPolicyId.HasValue)) return "VEHICLE_REGISTRATION_FROM_POLICY";
            if (intent.PolicyId.HasValue) return "POLICY_BY_ID";
            if (intent.ClaimId.HasValue) return "CLAIM_BY_ID";
            if (intent.UserId.HasValue && intent.IncludePolicies) return "USER_POLICIES";
            if (intent.UserId.HasValue && intent.IncludeClaims) return "USER_CLAIMS";
            if (intent.IncludeReferrals) return "REFERRAL_REVIEW";
            return "GENERAL";
        }

        private static bool ContainsAny(string input, params string[] keywords)
        {
            return keywords.Any(input.Contains);
        }

        private static int? TryExtractId(Regex regex, string input)
        {
            var match = regex.Match(input);
            if (!match.Success)
            {
                return null;
            }

            return int.TryParse(match.Groups[1].Value, out var id) ? id : null;
        }

        private static bool TryParseContextEntityId(string? entityId, out int id)
        {
            return int.TryParse(entityId, out id);
        }

        private static string ResolveReferencedEntityType(string lowerQuestion, string fallback)
        {
            if (ContainsAny(lowerQuestion, "claim", "rejected", "rejection", "fraud"))
            {
                return "claim";
            }

            if (ContainsAny(lowerQuestion, "policy", "premium", "coverage", "plan", "idv"))
            {
                return "policy";
            }

            if (ContainsAny(lowerQuestion, "user", "customer", "officer", "admin"))
            {
                return "user";
            }

            return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback.Trim().ToLowerInvariant();
        }

        private static ContextMemoryDto BuildContextMemory(IReadOnlyList<string>? history)
        {
            var memory = new ContextMemoryDto();
            if (history == null || history.Count == 0)
            {
                return memory;
            }

            for (var i = history.Count - 1; i >= 0; i--)
            {
                var entry = history[i];
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                var normalized = entry.Trim();
                var lower = normalized.ToLowerInvariant();

                var claimId = TryExtractId(ClaimIdRegex, normalized);
                var policyId = TryExtractId(PolicyIdRegex, normalized);
                var userId = TryExtractId(UserIdRegex, normalized);

                if (claimId.HasValue)
                {
                    memory.LastEntityId = claimId.Value.ToString();
                    memory.EntityType = "claim";
                }
                else if (policyId.HasValue)
                {
                    memory.LastEntityId = policyId.Value.ToString();
                    memory.EntityType = "policy";
                }
                else if (userId.HasValue)
                {
                    memory.LastEntityId = userId.Value.ToString();
                    memory.EntityType = "user";
                }

                if (string.IsNullOrWhiteSpace(memory.LastIntent) || memory.LastIntent == "GENERAL")
                {
                    var inferredIntent = InferIntentTypeFromText(lower);
                    if (!string.IsNullOrWhiteSpace(inferredIntent))
                    {
                        memory.LastIntent = inferredIntent;
                    }
                }

                if (!string.IsNullOrWhiteSpace(memory.EntityType) && !string.IsNullOrWhiteSpace(memory.LastIntent) && memory.LastIntent != "GENERAL")
                {
                    break;
                }
            }

            return memory;
        }

        private static string InferIntentTypeFromText(string lower)
        {
            if (ContainsAny(lower, "claim", "rejected", "rejection", "fraud", "approved")) return "CLAIM";
            if (ContainsAny(lower, "policy", "premium", "coverage", "plan", "idv")) return "POLICY";
            if (ContainsAny(lower, "referral", "referrer", "referee", "code")) return "REFERRAL";
            if (ContainsAny(lower, "payment", "invoice", "transaction", "collected")) return "PAYMENT";
            if (ContainsAny(lower, "vehicle", "registration", "car")) return "VEHICLE";
            if (ContainsAny(lower, "user", "customer", "officer", "admin")) return "USER";
            return "GENERAL";
        }

        private static ContextMemoryDto BuildCurrentContextMemory(IntentResultDto current, ContextMemoryDto previous)
        {
            var memory = new ContextMemoryDto
            {
                LastIntent = string.IsNullOrWhiteSpace(current.IntentType) ? previous.LastIntent : current.IntentType,
                LastEntityId = previous.LastEntityId,
                EntityType = previous.EntityType,
                AnchorPolicyId = previous.AnchorPolicyId,
                AnchorVehicleId = previous.AnchorVehicleId,
                LastPlanType = string.IsNullOrWhiteSpace(current.QueryPlanHint) ? previous.LastPlanType : current.QueryPlanHint,
                LastRangeDays = current.RequestedRangeDays ?? previous.LastRangeDays,
                LastUpdatedUtc = DateTime.UtcNow
            };

            if (current.ClaimId.HasValue)
            {
                memory.LastEntityId = current.ClaimId.Value.ToString();
                memory.EntityType = "claim";
            }
            else if (current.PolicyId.HasValue)
            {
                memory.LastEntityId = current.PolicyId.Value.ToString();
                memory.EntityType = "policy";
                memory.AnchorPolicyId = current.PolicyId.Value;
            }
            else if (current.UserId.HasValue)
            {
                memory.LastEntityId = current.UserId.Value.ToString();
                memory.EntityType = "user";
            }

            return memory;
        }

        private static void ApplyIntentIncludes(
            string intentType,
            ref bool includeClaims,
            ref bool includeUsers,
            ref bool includePolicies,
            ref bool includeReferrals,
            ref bool includeVehicles,
            ref bool includePayments,
            ref bool includeApplications,
            ref bool includeGarages,
            ref bool includeNotifications)
        {
            switch ((intentType ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "CLAIM":
                    includeClaims = true;
                    break;
                case "POLICY":
                    includePolicies = true;
                    break;
                case "USER":
                    includeUsers = true;
                    break;
                case "REFERRAL":
                    includeReferrals = true;
                    break;
                case "VEHICLE":
                    includeVehicles = true;
                    break;
                case "PAYMENT":
                    includePayments = true;
                    break;
                case "APPLICATION":
                    includeApplications = true;
                    break;
                case "GARAGE":
                    includeGarages = true;
                    break;
                case "NOTIFICATION":
                    includeNotifications = true;
                    break;
            }
        }

        private static string ResolveIntentType(IntentResultDto intent)
        {
            if (intent.IncludeClaims && !intent.IncludePolicies && !intent.IncludeUsers && !intent.IncludeReferrals)
            {
                return "CLAIM";
            }

            if (intent.IncludeReferrals && !intent.IncludeClaims && !intent.IncludePolicies)
            {
                return "REFERRAL";
            }

            if (intent.IncludePolicies && !intent.IncludeClaims && !intent.IncludeReferrals)
            {
                return "POLICY";
            }

            if (intent.IncludeUsers && !intent.IncludeClaims && !intent.IncludePolicies && !intent.IncludeReferrals)
            {
                return "USER";
            }

            if (intent.IncludeVehicles && !intent.IncludeClaims && !intent.IncludeUsers && !intent.IncludePolicies && !intent.IncludeReferrals)
            {
                return "VEHICLE";
            }

            if (intent.IncludePayments && !intent.IncludeClaims && !intent.IncludeUsers && !intent.IncludePolicies && !intent.IncludeReferrals)
            {
                return "PAYMENT";
            }

            if (intent.IncludeApplications && !intent.IncludeClaims && !intent.IncludeUsers && !intent.IncludePolicies && !intent.IncludeReferrals)
            {
                return "APPLICATION";
            }

            if (intent.IncludeGarages && !intent.IncludeClaims && !intent.IncludeUsers && !intent.IncludePolicies && !intent.IncludeReferrals)
            {
                return "GARAGE";
            }

            if (intent.IncludeNotifications && !intent.IncludeClaims && !intent.IncludeUsers && !intent.IncludePolicies && !intent.IncludeReferrals)
            {
                return "NOTIFICATION";
            }

            return "MIXED";
        }
    }
}
