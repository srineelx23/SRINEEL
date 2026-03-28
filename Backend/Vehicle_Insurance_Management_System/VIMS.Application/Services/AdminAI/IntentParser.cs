using System.Text.RegularExpressions;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;

namespace VIMS.Application.Services.AdminAI
{
    public class IntentParser : IIntentParser
    {
        private static readonly Regex ClaimIdRegex = new(@"\bclaim(?:\s*(?:id|number|#))?\s*[:#-]?\s*(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UserIdRegex = new(@"\buser(?:\s*(?:id|#))?\s*[:#-]?\s*(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PolicyIdRegex = new(@"\bpolicy(?:\s*(?:id|number|#))?\s*[:#-]?\s*(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IntentResultDto Parse(string question)
        {
            var normalized = (question ?? string.Empty).Trim();
            var lower = normalized.ToLowerInvariant();

            var includeClaims = ContainsAny(lower, "claim", "rejected", "rejection", "approved", "fraud");
            var includeUsers = ContainsAny(lower, "user", "customer", "officer", "admin");
            var includePolicies = ContainsAny(lower, "policy", "eligible", "coverage", "plan");
            var includeReferrals = ContainsAny(lower, "referral", "refer", "abuse", "referrer", "referee", "code");
            var includeVehicles = ContainsAny(lower, "vehicle", "registration", "car", "idv");
            var includePayments = ContainsAny(lower, "payment", "premium", "invoice", "transaction", "collected");
            var includeApplications = ContainsAny(lower, "application", "applied", "under review", "transfer");
            var includeGarages = ContainsAny(lower, "garage", "workshop", "service center", "service centre");
            var includeNotifications = ContainsAny(lower, "notification", "notifications", "alert", "message");

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
                ClaimId = TryExtractId(ClaimIdRegex, normalized),
                UserId = TryExtractId(UserIdRegex, normalized),
                PolicyId = TryExtractId(PolicyIdRegex, normalized)
            };

            result.IntentType = ResolveIntentType(result);
            return result;
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
