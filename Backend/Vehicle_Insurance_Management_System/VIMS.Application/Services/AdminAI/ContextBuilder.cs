using System.Text.Json;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;

namespace VIMS.Application.Services.AdminAI
{
    public class ContextBuilder : IContextBuilder
    {
        public ContextDataDto Build(
            IntentResultDto intent,
            IReadOnlyList<ClaimContextDto> claims,
            IReadOnlyList<UserContextDto> users,
            IReadOnlyList<PolicyContextDto> policies,
            IReadOnlyList<VehicleContextDto> vehicles,
            IReadOnlyList<PaymentContextDto> payments,
            PaymentAggregateContextDto? paymentAggregates,
            IReadOnlyList<VehicleApplicationContextDto> applications,
            IReadOnlyList<GarageContextDto> garages,
            IReadOnlyList<NotificationContextDto> notifications,
            IReadOnlyList<ReferralContextDto> referrals,
            IReadOnlyList<ReferralAbuseSignalDto> referralAbuseSignals,
            IReadOnlyList<string> ragRules)
        {
            var dbPayload = new
            {
                intent = new
                {
                    intent.IntentType,
                    intent.ClaimId,
                    intent.UserId,
                    intent.PolicyId,
                    intent.IncludeVehicles,
                    intent.IncludePayments,
                    intent.IncludeApplications,
                    intent.IncludeGarages,
                    intent.IncludeNotifications
                },
                claims,
                users,
                policies,
                vehicles,
                payments,
                paymentAggregates,
                applications,
                garages,
                notifications,
                referrals,
                referralAbuseSignals
            };

            var dbJson = JsonSerializer.Serialize(dbPayload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var rules = ragRules
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var dataUsed = new List<string>();
            if (claims.Count > 0) dataUsed.Add("claims");
            if (users.Count > 0) dataUsed.Add("users");
            if (policies.Count > 0) dataUsed.Add("policies");
            if (vehicles.Count > 0) dataUsed.Add("vehicles");
            if (payments.Count > 0) dataUsed.Add("payments");
            if (paymentAggregates != null) dataUsed.Add("paymentAggregates");
            if (applications.Count > 0) dataUsed.Add("applications");
            if (garages.Count > 0) dataUsed.Add("garages");
            if (notifications.Count > 0) dataUsed.Add("notifications");
            if (referrals.Count > 0) dataUsed.Add("referrals");
            if (referralAbuseSignals.Count > 0) dataUsed.Add("referralAbuseSignals");

            return new ContextDataDto
            {
                DbJson = dbJson,
                RulesText = rules.Count > 0 ? string.Join("\n\n", rules) : "No matching business rules found.",
                DataUsed = dataUsed,
                RulesUsed = rules
            };
        }
    }
}
