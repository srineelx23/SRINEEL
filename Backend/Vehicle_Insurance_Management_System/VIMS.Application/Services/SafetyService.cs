using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;

namespace VIMS.Application.Services
{
    public class SafetyService : ISafetyService
    {
        private readonly IGeminiService _geminiService;

        public SafetyService(IGeminiService geminiService)
        {
            _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
        }

        public async Task<SafetyClassificationDTO> ClassifyQueryAsync(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new SafetyClassificationDTO { Type = "SAFE_QUERY", Intent = "UNKNOWN" };
            }

            var prompt = $@"
You are a strict classifier.

Return JSON:

{{
  ""type"": ""SAFE_QUERY or SENSITIVE_QUERY"",
  ""intent"": ""POLICY_INFO | CLAIM_PROCESS | TERM_EXPLANATION | UNKNOWN""
}}

Sensitive includes:
- other customers
- internal data
- admin data

Query to classify:
{query}
";

            var resultStr = await _geminiService.GenerateAnswerAsync(prompt, cancellationToken);

            if (string.IsNullOrWhiteSpace(resultStr))
            {
                return new SafetyClassificationDTO { Type = "SAFE_QUERY", Intent = "UNKNOWN" };
            }

            // Strip Markdown formatting if LLM wraps JSON inside triple backticks
            var jsonString = resultStr.Trim();
            if (jsonString.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                jsonString = jsonString.Substring(7);
            }
            else if (jsonString.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                jsonString = jsonString.Substring(3);
            }

            if (jsonString.EndsWith("```"))
            {
                jsonString = jsonString.Substring(0, jsonString.Length - 3);
            }

            jsonString = jsonString.Trim();

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<SafetyClassificationDTO>(jsonString, options);
                
                return parsed ?? new SafetyClassificationDTO { Type = "SAFE_QUERY", Intent = "UNKNOWN" };
            }
            catch
            {
                // Fallback robustly
                return new SafetyClassificationDTO { Type = "SAFE_QUERY", Intent = "UNKNOWN" };
            }
        }
    }
}
