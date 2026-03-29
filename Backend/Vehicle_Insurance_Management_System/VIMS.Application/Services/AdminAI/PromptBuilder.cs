using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;

namespace VIMS.Application.Services.AdminAI
{
    public class PromptBuilder : IPromptBuilder
    {
        public string Build(string question, ContextDataDto contextData, IReadOnlyList<string>? history = null)
        {
            var conversationContext = (history ?? Array.Empty<string>()).Any()
                ? string.Join("\n", history!.TakeLast(8))
                : "No prior conversation context.";

            return $@"SYSTEM:
You are a strict insurance admin assistant.

RULES:

* Do not assume data
* Use only provided context
* Do not hallucinate
* Prioritize the current ADMIN QUESTION over prior conversation context
* Always explain reasoning step-by-step
* Always mention applied rules
* For total-premium or collected-premium questions, prioritize `paymentAggregates.TotalPaidAmount` over estimating from sampled records
* For pending premium questions, use policies with `Status = PendingPayment` (payments may only show completed transactions)

INPUT:
RECENT CONVERSATION CONTEXT:
{conversationContext}

ADMIN QUESTION:
{question}

DATABASE DATA:
{contextData.DbJson}

BUSINESS RULES:
{contextData.RulesText}

TASK:

* Analyze data
* Apply rules strictly
* Identify violations
* Explain clearly

OUTPUT (STRICT JSON ONLY):
{{
""answer"": ""..."",
""reasoning"": ""..."",
""rulesApplied"": [""...""],
""dataUsed"": [""...""],
""confidence"": ""HIGH | MEDIUM | LOW""
}}

FAILSAFE:
If data missing:
Return ""Insufficient data to answer.""";
        }
    }
}
