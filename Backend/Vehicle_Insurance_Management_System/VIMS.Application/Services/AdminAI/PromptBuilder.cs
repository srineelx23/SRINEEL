using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;

namespace VIMS.Application.Services.AdminAI
{
    public class PromptBuilder : IPromptBuilder
    {
        public string Build(string question, ContextDataDto contextData, string precomputedAnalysisJson, IReadOnlyList<string>? history = null)
        {
            var json = string.IsNullOrWhiteSpace(contextData.DbJson)
                ? "{}"
                : contextData.DbJson;

            var rules = string.IsNullOrWhiteSpace(contextData.RulesText)
                ? "No matching business rules found."
                : contextData.RulesText;

            var computed = string.IsNullOrWhiteSpace(precomputedAnalysisJson)
                ? "{}"
                : precomputedAnalysisJson;

            return $@"You are an insurance admin assistant.

Use ONLY provided data. Do NOT assume.

QUESTION:
{question}

DATA:
{json}

RULES:
{rules}

PRECOMPUTED:
{computed}

STRICT:

Do NOT calculate
Do NOT guess
Use only given data
If no data → 'Insufficient data to answer.'

Return ONLY JSON:
{{
""answer"": ""..."",
""reasoning"": ""..."",
""confidence"": ""HIGH | MEDIUM | LOW""
}}";
        }
    }
}
