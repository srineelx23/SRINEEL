using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IBusinessRuleEvaluatorService
    {
        BusinessRuleAnalysisDto Evaluate(
            IntentResultDto intent,
            IReadOnlyList<ClaimContextDto> claims,
            IReadOnlyList<PolicyContextDto> policies,
            IReadOnlyList<PaymentContextDto> payments,
            PaymentAggregateContextDto? paymentAggregates);
    }
}