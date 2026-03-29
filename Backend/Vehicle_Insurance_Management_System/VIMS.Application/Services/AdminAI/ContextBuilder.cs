using System.Text.Json;
using System.Text.Json.Serialization;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;

namespace VIMS.Application.Services.AdminAI
{
    public class ContextBuilder : IContextBuilder
    {
        private const int PreferredContextCharLimit = 1200;

        public ContextDataDto Build(
            IntentResultDto intent,
            IReadOnlyList<ClaimContextDto> claims,
            IReadOnlyList<UserContextDto> users,
            IReadOnlyList<PolicyContextDto> policies,
            IReadOnlyList<VehicleContextDto> vehicles,
            IReadOnlyList<PaymentContextDto> payments,
            PaymentAggregateContextDto? paymentAggregates,
            IReadOnlyDictionary<string, object> computed,
            IReadOnlyList<VehicleApplicationContextDto> applications,
            IReadOnlyList<GarageContextDto> garages,
            IReadOnlyList<NotificationContextDto> notifications,
            IReadOnlyList<ReferralContextDto> referrals,
            IReadOnlyList<ReferralAbuseSignalDto> referralAbuseSignals,
            IReadOnlyList<string> ragRules)
        {
            _ = intent;
            _ = users;
            _ = vehicles;
            _ = payments;
            _ = paymentAggregates;
            _ = applications;
            _ = garages;
            _ = notifications;
            _ = referrals;
            _ = referralAbuseSignals;

            var compactClaims = claims
                .Take(5)
                .Select(c => new
            {
                c.ClaimId,
                c.PolicyId,
                c.Status,
                c.RejectionReason
            });

            var compactPolicies = policies
                .Take(5)
                .Select(p => new
            {
                p.PolicyId,
                p.IDV,
                p.PremiumAmount,
                p.VehicleType,
                p.FuelType,
                p.VehicleRegistrationNumber
            });

            var compactClaimsList = compactClaims.ToList();
            var compactPoliciesList = compactPolicies.ToList();

            var coreComputed = new Dictionary<string, object>();
            if (computed.TryGetValue("highestIDV", out var highestIdv)) coreComputed["highestIDV"] = highestIdv;
            if (computed.TryGetValue("totalPremium", out var totalPremium)) coreComputed["totalPremium"] = totalPremium;
            if (computed.TryGetValue("pendingCount", out var pendingCount)) coreComputed["pendingCount"] = pendingCount;

            computed.TryGetValue("topPolicy", out var topPolicy);
            computed.TryGetValue("topClaimVehicle", out var topClaimVehicle);

            var payload = new
            {
                computed = coreComputed,
                topResult = new
                {
                    topPolicy,
                    topClaimVehicle
                },
                summary = new
                {
                    totalClaims = claims.Count,
                    totalPolicies = policies.Count,
                    claims = compactClaimsList,
                    policies = compactPoliciesList
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var dbJson = JsonSerializer.Serialize(payload, jsonOptions);

            if (dbJson.Length > PreferredContextCharLimit)
            {
                var reducedPayload = new
                {
                    computed = coreComputed,
                    topResult = new
                    {
                        topPolicy,
                        topClaimVehicle
                    },
                    summary = new
                    {
                        totalClaims = claims.Count,
                        totalPolicies = policies.Count,
                        claims = compactClaimsList.Take(3).ToList(),
                        policies = compactPoliciesList.Take(3).ToList()
                    }
                };

                dbJson = JsonSerializer.Serialize(reducedPayload, jsonOptions);
            }

            if (dbJson.Length > PreferredContextCharLimit)
            {
                var minimalPayload = new
                {
                    computed = coreComputed,
                    topResult = new
                    {
                        topPolicy,
                        topClaimVehicle
                    },
                    summary = new
                    {
                        totalClaims = claims.Count,
                        totalPolicies = policies.Count
                    }
                };

                dbJson = JsonSerializer.Serialize(minimalPayload, jsonOptions);
            }

            if (dbJson.Length > PreferredContextCharLimit)
            {
                var hardMinimalPayload = new
                {
                    computed = coreComputed,
                    topResult = new { },
                    summary = new
                    {
                        totalClaims = claims.Count,
                        totalPolicies = policies.Count
                    }
                };

                dbJson = JsonSerializer.Serialize(hardMinimalPayload, jsonOptions);
            }

            var rules = ragRules
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var dataUsed = new List<string>();
            if (coreComputed.Count > 0) dataUsed.Add("computed");
            dataUsed.Add("topResult");
            dataUsed.Add("summary");

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
