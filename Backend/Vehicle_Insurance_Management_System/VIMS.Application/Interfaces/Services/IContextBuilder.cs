using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IContextBuilder
    {
        ContextDataDto Build(
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
            IReadOnlyList<string> ragRules);
    }
}
