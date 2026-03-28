using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.Application.Services
{
    public class ChatService : IChatService
    {
        private readonly ISafetyService _safetyService;
        private readonly IRAGService _ragService;
        private readonly IGeminiService _geminiService;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorSearchService _vectorSearchService;
        private readonly IPolicyPlanRepository _policyPlanRepository;

        public ChatService(
            ISafetyService safetyService,
            IRAGService ragService,
            IGeminiService geminiService,
            IEmbeddingService embeddingService,
            IVectorSearchService vectorSearchService,
            IPolicyPlanRepository policyPlanRepository)
        {
            _safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
            _ragService = ragService ?? throw new ArgumentNullException(nameof(ragService));
            _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
            _vectorSearchService = vectorSearchService ?? throw new ArgumentNullException(nameof(vectorSearchService));
            _policyPlanRepository = policyPlanRepository ?? throw new ArgumentNullException(nameof(policyPlanRepository));
        }

        public async Task<string> AnswerQueryAsync(
            string query,
            IReadOnlyList<ChatHistoryItemDTO>? history = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "Please provide a valid question.";
            }

            // 1. Classify query using SafetyService
            var classification = await _safetyService.ClassifyQueryAsync(query, cancellationToken);

            // 2. If SENSITIVE_QUERY -> Return rejection
            if (string.Equals(classification?.Type, "SENSITIVE_QUERY", StringComparison.OrdinalIgnoreCase))
            {
                return "Sorry, I cannot provide that information.";
            }

            // Enforce hard eligibility rules from business context before generating recommendations.
            if (TryExtractVehicleYear(query, history, out var vehicleYear))
            {
                var currentYear = DateTime.UtcNow.Year;
                var vehicleAge = Math.Max(0, currentYear - vehicleYear);
                if (vehicleAge > 15)
                {
                    return $"Your vehicle appears to be {vehicleAge} years old (model year {vehicleYear}). Vehicles older than 15 years are not eligible for policy issuance in this system, so I cannot recommend a plan.";
                }
            }

            // 3. Else -> Retrieve business-rule RAG and policy-plan RAG contexts
            var businessRuleChunks = await TryRetrieveBusinessRulesRagContextAsync(query, cancellationToken);
            var businessRulesContext = string.Join("\n\n", businessRuleChunks);

            var allPlans = await _policyPlanRepository.GetAllAsync();
            var activePlans = allPlans
                .Where(p => p.Status == PlanStatus.Active)
                .ToList();

            var policyPlanChunks = await TryRetrievePolicyPlanRagContextAsync(query, activePlans, cancellationToken);
            var policyPlansContext = string.Join("\n\n", policyPlanChunks);
            var fullPolicyPlanCatalog = BuildFullPolicyPlanCatalog(activePlans);

            var conversationContext = BuildConversationContext(history);

            // Build prompt with context
            var prompt = $@"
You are a vehicle insurance assistant.

STRICT RULES:
- Answer ONLY from the provided context below.
- No external knowledge
- No sensitive data
- If not found → say ""I don’t have that information""
- Business rules always take priority over policy plan suggestions.
- If any business rule says the user is not eligible, clearly state ineligibility and do not recommend any plan.
- When model year is available, compute vehicle age using current year {DateTime.UtcNow.Year}. If age is greater than 15, respond with ineligibility.
- If the question is about policy plan suitability/recommendation, use ONLY policy plan context.
- If the question is about business rules, use ONLY business rules context.
- If the question needs both, combine both contexts.
- For yes/no coverage questions, begin with ""Yes"" or ""No"".
- Do not invent plan names, features, prices, or rules.
- Never assume missing user details. If key details (for example vehicle type) are missing for a recommendation, ask one concise follow-up question.
- Use Conversation History to resolve short follow-up replies like ""Diesel"", ""Car"", or ""Petrol"".
- Do not ask the same clarification repeatedly if the user already provided it in Conversation History.
- For plan filtering questions (for example budget limits, own-damage + third-party combinations, cheapest/highest), evaluate against the full policy catalog before finalizing the answer.
- If at least one matching plan exists in the provided catalog, do not answer ""No"".
- If user asks for unsupported domains (for example airplane/aircraft/marine), answer: ""I don't have that information. I can assist only with two-wheelers, cars/four-wheelers, and heavy vehicles covered by this system.""

Conversation History:
{conversationContext}

Business Rules Context:
{businessRulesContext}

Policy Plan RAG Context:
{policyPlansContext}

Full Active Policy Plan Catalog:
{fullPolicyPlanCatalog}

User Question:
{query}
";

            // Call GeminiService
            var response = await _geminiService.GenerateAnswerAsync(prompt, cancellationToken);

            if (string.IsNullOrWhiteSpace(response))
            {
                return "I don't have that information.";
            }

            return SanitizeForCustomerResponse(response);
        }

        private async Task<List<string>> TryRetrieveBusinessRulesRagContextAsync(string query, CancellationToken cancellationToken)
        {
            try
            {
                return await _ragService.RetrieveAsync(query, cancellationToken);
            }
            catch
            {
                return new List<string>();
            }
        }

        private async Task<List<string>> TryRetrievePolicyPlanRagContextAsync(
            string query,
            IReadOnlyList<PolicyPlan> activePlans,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query) || activePlans.Count == 0)
            {
                return new List<string>();
            }

            try
            {
                var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
                if (queryEmbedding.Length == 0)
                {
                    return new List<string>();
                }

                var candidates = new List<(RagChunkDTO Chunk, float[] Embedding)>();
                foreach (var plan in activePlans)
                {
                    var planChunkText = BuildPolicyPlanChunkText(plan);
                    var embedding = await _embeddingService.GenerateEmbeddingAsync(planChunkText, cancellationToken);
                    if (embedding.Length == 0)
                    {
                        continue;
                    }

                    candidates.Add((
                        new RagChunkDTO
                        {
                            SourceType = "PolicyPlan",
                            SourceId = plan.PlanId.ToString(),
                            Text = planChunkText,
                            Similarity = 0f
                        },
                        embedding));
                }

                if (candidates.Count == 0)
                {
                    return new List<string>();
                }

                var topMatches = _vectorSearchService.GetTopMatches(queryEmbedding, candidates, 8);
                return topMatches
                    .Select(x => x.Text)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static string BuildPolicyPlanChunkText(PolicyPlan plan)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"PlanName: {plan.PlanName}");
            builder.AppendLine($"PolicyType: {plan.PolicyType}");
            builder.AppendLine($"ApplicableVehicleType: {plan.ApplicableVehicleType}");
            builder.AppendLine($"BasePremium: INR {plan.BasePremium:0.##}");
            builder.AppendLine($"DurationMonths: {plan.PolicyDurationMonths}");
            builder.AppendLine($"DeductibleAmount: INR {plan.DeductibleAmount:0.##}");
            builder.AppendLine($"MaxCoverageAmount: INR {plan.MaxCoverageAmount:0.##}");
            builder.AppendLine($"Coverage -> ThirdParty: {ToYesNo(plan.CoversThirdParty)}, OwnDamage: {ToYesNo(plan.CoversOwnDamage)}, Theft: {ToYesNo(plan.CoversTheft)}");
            builder.AppendLine($"Addons -> ZeroDepreciation: {ToYesNo(plan.ZeroDepreciationAvailable)}, EngineProtection: {ToYesNo(plan.EngineProtectionAvailable)}, RoadsideAssistance: {ToYesNo(plan.RoadsideAssistanceAvailable)}");
            return builder.ToString().Trim();
        }

        private static string ToYesNo(bool value)
        {
            return value ? "Yes" : "No";
        }

        private static string SanitizeForCustomerResponse(string response)
        {
            var cleaned = response.Trim();

            // Never expose internal PlanId references to customers.
            cleaned = Regex.Replace(cleaned, @"\s*\(?\s*PlanId\s*:\s*\d+\s*\)?", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s*\[Note:\s*Answered by.*?\]", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");

            return cleaned;
        }

        private static string BuildFullPolicyPlanCatalog(IReadOnlyList<PolicyPlan> plans)
        {
            if (plans.Count == 0)
            {
                return "No active policy plans found.";
            }

            var lines = plans
                .OrderBy(p => p.ApplicableVehicleType)
                .ThenBy(p => p.BasePremium)
                .Select(p =>
                    $"PlanName={p.PlanName}; VehicleType={p.ApplicableVehicleType}; PolicyType={p.PolicyType}; BasePremium=INR {p.BasePremium:0.##}; DurationMonths={p.PolicyDurationMonths}; CoversThirdParty={ToYesNo(p.CoversThirdParty)}; CoversOwnDamage={ToYesNo(p.CoversOwnDamage)}; CoversTheft={ToYesNo(p.CoversTheft)}; ZeroDepreciation={ToYesNo(p.ZeroDepreciationAvailable)}; EngineProtection={ToYesNo(p.EngineProtectionAvailable)}; RoadsideAssistance={ToYesNo(p.RoadsideAssistanceAvailable)}")
                .ToList();

            return string.Join("\n", lines);
        }

        private static string BuildConversationContext(IReadOnlyList<ChatHistoryItemDTO>? history)
        {
            if (history == null || history.Count == 0)
            {
                return "No prior conversation available.";
            }

            var recent = history
                .Where(h => h != null && !string.IsNullOrWhiteSpace(h.Role) && !string.IsNullOrWhiteSpace(h.Content))
                .TakeLast(12)
                .ToList();

            if (recent.Count == 0)
            {
                return "No prior conversation available.";
            }

            var builder = new StringBuilder();
            foreach (var turn in recent)
            {
                var role = turn.Role.Trim().ToLowerInvariant() switch
                {
                    "assistant" => "Assistant",
                    "bot" => "Assistant",
                    _ => "User"
                };

                builder.AppendLine($"{role}: {turn.Content.Trim()}");
            }

            return builder.ToString().Trim();
        }

        private static bool TryExtractVehicleYear(
            string query,
            IReadOnlyList<ChatHistoryItemDTO>? history,
            out int vehicleYear)
        {
            vehicleYear = 0;

            var combinedParts = new List<string>();
            if (history != null && history.Count > 0)
            {
                combinedParts.AddRange(history
                    .Where(h => h != null && !string.IsNullOrWhiteSpace(h.Content))
                    .Select(h => h.Content));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                combinedParts.Add(query);
            }

            if (combinedParts.Count == 0)
            {
                return false;
            }

            var combined = string.Join(" ", combinedParts);
            var matches = Regex.Matches(combined, @"\b(19\d{2}|20\d{2})\b");
            if (matches.Count == 0)
            {
                return false;
            }

            var currentYear = DateTime.UtcNow.Year;
            foreach (Match match in matches.Cast<Match>().Reverse())
            {
                if (int.TryParse(match.Value, out var parsedYear) && parsedYear >= 1980 && parsedYear <= currentYear)
                {
                    vehicleYear = parsedYear;
                    return true;
                }
            }

            return false;
        }
    }
}
