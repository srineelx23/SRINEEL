using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;

namespace VIMS.Application.Services.AdminAI
{
    public class PromptBuilder : IPromptBuilder
    {
        public string Build(string question, ContextDataDto contextData)
        {
            return $@"SYSTEM:
You are a strict insurance admin assistant.

RULES:

* Do not assume data
* Use only provided context
* Do not hallucinate
* Always explain reasoning step-by-step
* Always mention applied rules
* For total-premium or collected-premium questions, prioritize `paymentAggregates.TotalPaidAmount` over estimating from sampled records

INPUT:
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
