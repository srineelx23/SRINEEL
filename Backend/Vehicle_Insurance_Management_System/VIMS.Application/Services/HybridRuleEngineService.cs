using System.Text.Json;
using Microsoft.Extensions.Hosting;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;

namespace VIMS.Application.Services
{
    public class HybridRuleEngineService : IHybridRuleEngineService
    {
        private const string FallbackAnswer = "I don't have that information";

        private readonly List<RuleDefinition> _rules;
        private readonly IGeminiService _geminiService;

        public HybridRuleEngineService(IHostEnvironment env, IGeminiService geminiService)
        {
            var path = Path.Combine(env.ContentRootPath, "rules.json");
            _rules = LoadRules(path)
                .OrderByDescending(r => r.Priority)
                .ToList();

            _geminiService = geminiService;
        }

        public async Task<string> ExecuteAsync(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return FallbackAnswer;
            }

            var normalized = NormalizeQuery(query);

            foreach (var rule in _rules)
            {
                if (rule.Keywords.Count == 0 || string.IsNullOrWhiteSpace(rule.Response))
                {
                    continue;
                }

                if (rule.Keywords.All(k => !string.IsNullOrWhiteSpace(k) && normalized.Contains(NormalizeQuery(k))))
                {
                    return rule.Response;
                }
            }

            var semanticMatch = await MatchRuleUsingLLM(query, cancellationToken);
            if (!string.IsNullOrWhiteSpace(semanticMatch))
            {
                return semanticMatch;
            }

            return await GenerateSafeLLMResponse(query, cancellationToken);
        }

        private async Task<string?> MatchRuleUsingLLM(string query, CancellationToken cancellationToken)
        {
            if (_rules.Count == 0)
            {
                return null;
            }

            var rulesList = string.Join("\n", _rules.Select((r, i) => $"{i}: {string.Join(", ", r.Keywords)}"));

            var prompt = $"""
You are a classifier.

Match the USER_QUERY to the closest rule.

RULES:
{rulesList}

INSTRUCTIONS:
- Return ONLY the index of the best matching rule
- If no rule matches, return: NONE

USER_QUERY:
{query}
""";

            var response = await _geminiService.GenerateAnswerAsync(prompt, cancellationToken);
            if (string.IsNullOrWhiteSpace(response))
            {
                return null;
            }

            var normalizedResponse = response.Trim();
            if (normalizedResponse.Contains('\n'))
            {
                normalizedResponse = normalizedResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            }

            if (int.TryParse(normalizedResponse, out var index) && index >= 0 && index < _rules.Count)
            {
                return _rules[index].Response;
            }

            return null;
        }

        private async Task<string> GenerateSafeLLMResponse(string query, CancellationToken cancellationToken)
        {
            var rulesSummary = string.Join("\n", _rules.Select(r => $"- {r.Response}"));

            var prompt = $"""
You are a vehicle insurance assistant for a specific system.

SYSTEM RULES (STRICT - DO NOT VIOLATE):
{rulesSummary}

INSTRUCTIONS:
- Always follow the system rules above
- If the question relates to any rule, strictly use the system rule
- Do NOT contradict system rules
- Do NOT assume generic insurance behavior if rules are defined
- If unsure, respond: I don't have that information

QUESTION:
{query}
""";

            var response = await _geminiService.GenerateAnswerAsync(prompt, cancellationToken);
            if (string.IsNullOrWhiteSpace(response))
            {
                return FallbackAnswer;
            }

            return response.Trim();
        }

        private static List<RuleDefinition> LoadRules(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new List<RuleDefinition>();
                }

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<RuleDefinition>>(json) ?? new List<RuleDefinition>();
            }
            catch
            {
                return new List<RuleDefinition>();
            }
        }

        private static string NormalizeQuery(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var lowered = value.ToLowerInvariant()
                .Replace("3rd", "third")
                .Replace("tp", "third party");

            var chars = lowered
                .Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ')
                .ToArray();

            return string.Join(" ", new string(chars)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
