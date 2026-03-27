using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;

namespace VIMS.Application.Services
{
    public class VertexAgentService : IVertexAgentService
    {
        private const string EndpointTemplate = "https://us-central1-aiplatform.googleapis.com/v1/projects/vims-chatbot-ai/locations/us-central1/publishers/google/models/gemini-2.5-flash:generateContent";
        private const string CloudPlatformScope = "https://www.googleapis.com/auth/cloud-platform";

        private static readonly JsonSerializerOptions RequestJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly JsonSerializerOptions ResponseJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IHttpClientFactory _httpClientFactory;

        public VertexAgentService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<AgentDecision?> GetDecisionAsync(string query, CancellationToken cancellationToken = default)
        {
            AgentDecision? ReturnNullDecision(string reason)
            {
                Console.WriteLine("VertexAgentService returning NULL decision: " + reason);
                return null;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return ReturnNullDecision("query is empty");
            }

            Console.WriteLine("PROJECT ID: " + Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT"));

            var projectId = ResolveProjectId();
            if (string.IsNullOrWhiteSpace(projectId))
            {
                return ReturnNullDecision("project id not resolved");
            }

            var accessToken = await GetAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return ReturnNullDecision("access token not available");
            }

            var prompt = BuildPrompt(query);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var endpoint = string.Format(EndpointTemplate, Uri.EscapeDataString(projectId));
            var payload = JsonSerializer.Serialize(requestBody, RequestJsonOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                using var response = await httpClient.SendAsync(request, cancellationToken);
                var raw = await response.Content.ReadAsStringAsync();
                Console.WriteLine("VERTEX STATUS: " + response.StatusCode);
                Console.WriteLine("VERTEX RESPONSE: " + raw);

                if (!response.IsSuccessStatusCode)
                {
                    return ReturnNullDecision("vertex API request failed");
                }

                using var document = JsonDocument.Parse(raw);

                var modelText = ExtractModelText(document.RootElement);
                if (string.IsNullOrWhiteSpace(modelText))
                {
                    return ReturnNullDecision("model text extraction failed");
                }

                var rawJson = ExtractJsonPayload(modelText);
                if (string.IsNullOrWhiteSpace(rawJson))
                {
                    return ReturnNullDecision("json payload extraction failed");
                }

                try
                {
                    var decision = JsonSerializer.Deserialize<AgentDecision>(rawJson, ResponseJsonOptions);
                    if (decision != null)
                    {
                        Console.WriteLine("PARSED DECISION: " + JsonSerializer.Serialize(decision));
                    }
                    else
                    {
                        Console.WriteLine("PARSED DECISION: null");
                    }
                    var normalizedDecision = ValidateAndNormalizeDecision(decision);
                    if (normalizedDecision != null)
                    {
                        Console.WriteLine(
                            "[VertexAgentService] query=\"{0}\", entity=\"{1}\", aggregation=\"{2}\", sortBy=\"{3}\", limit={4}",
                            query,
                            normalizedDecision.Entity,
                            normalizedDecision.Aggregation,
                            normalizedDecision.SortBy,
                            normalizedDecision.Limit);
                    }

                    if (normalizedDecision == null)
                    {
                        Console.WriteLine("VertexAgentService returning NULL decision");
                    }

                    return normalizedDecision;
                }
                catch (JsonException)
                {
                    return ReturnNullDecision("decision deserialization failed");
                }
            }
            catch (JsonException)
            {
                return ReturnNullDecision("vertex response JSON parse failed");
            }
            catch (HttpRequestException)
            {
                return ReturnNullDecision("http request exception");
            }
            catch (TaskCanceledException)
            {
                return ReturnNullDecision("request canceled or timed out");
            }
        }

        private static AgentDecision? ValidateAndNormalizeDecision(AgentDecision? decision)
        {
            if (decision == null)
            {
                return null;
            }

            decision.Intent = (decision.Intent ?? string.Empty).Trim();
            decision.Entity = (decision.Entity ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(decision.Entity))
            {
                return null;
            }

            decision.Aggregation = string.IsNullOrWhiteSpace(decision.Aggregation)
                ? "NONE"
                : decision.Aggregation.Trim();

            decision.SortBy = (decision.SortBy ?? string.Empty).Trim();
            decision.GroupBy = (decision.GroupBy ?? string.Empty).Trim();
            decision.Filters ??= new Dictionary<string, string>();

            return decision;
        }

        private static string BuildPrompt(string query)
        {
                        return $@"You are a deterministic query planner for a vehicle insurance system.

Your task:
- Convert USER_QUERY into exactly one JSON object.
- Return ONLY JSON.
- Do NOT return markdown, code fences, comments, or explanations.

Output schema (must match exactly):
{{
    ""intent"": ""DATA_QUERY | ANALYTICS | RECOMMENDATION | RULE_QUERY"",
    ""entity"": ""CLAIMS | POLICIES | PLANS | APPLICATIONS | PAYMENTS"",
    ""filters"": {{
        ""status"": """",
        ""dateFrom"": """",
        ""dateTo"": """",
        ""claimType"": """"
    }},
    ""aggregation"": ""NONE | SUM | COUNT | AVG | MAX | MIN"",
    ""groupBy"": """",
    ""sortBy"": """",
    ""limit"": 0
}}

IMPORTANT MAPPING RULES:

MUST ALWAYS map these keywords:
- count -> COUNT
- total -> SUM
- average -> AVG
- highest -> MAX
- lowest -> MIN

- If query contains ""top N"":
    -> aggregation = ""NONE""
    -> extract N and set limit = N
    -> sortBy must be set to the correct descending field for the selected entity
    -> limit must be N

- If query contains ""first"" or ""earliest"":
    -> sortBy = ""CreatedAt asc"" (or entity-equivalent date field)
    -> limit = 1

- If query contains ""latest"":
    -> sortBy = ""CreatedAt desc"" (or entity-equivalent date field)
    -> limit = 1

STRICT RULE:
- NEVER return aggregation as empty.
- ALWAYS choose the correct aggregation.
- aggregation must NEVER be ""NONE"" if any aggregation keyword exists.
- violation is not allowed.
- Precedence: explicit ranking phrases (""top N"", ""highest N"", ""max N"") override aggregation keywords and must use aggregation = ""NONE"".

Ranking field mapping guidance:
- policies + premium/highest premium -> sortBy = ""PremiumAmount desc"".
- claims + amount/payout/approved amount -> sortBy = ""ApprovedAmount desc"".
- plans + premium/highest premium -> sortBy = ""BasePremium desc"".
- payments + amount -> sortBy = ""Amount desc"".
- applications + invoice/value -> sortBy = ""InvoiceAmount desc"".
- claims/applications earliest-first queries -> sortBy = ""CreatedAt asc"".
- claims/applications latest queries -> sortBy = ""CreatedAt desc"".
- policies earliest-first queries -> sortBy = ""StartDate asc"".
- policies latest queries -> sortBy = ""StartDate desc"".

Ranking reliability rules:
- sortBy must always match a valid field for the selected entity.
- limit must always be set correctly for ranking intents (top N, first/earliest, latest).

Example:
- ""top 3 policies by premium"" -> entity = ""POLICIES"", sortBy = ""PremiumAmount desc"", limit = 3, aggregation = ""NONE"".

If no ranking intent is present, keep sortBy = """" and limit = 0.
If no aggregation intent is present, aggregation = ""NONE"".

Intent selection:
- DATA_QUERY: direct listing/filtering lookup.
- ANALYTICS: includes aggregation, ranking, comparison, totals, averages.
- RECOMMENDATION: asks for suggested/best plans or options.
- RULE_QUERY: asks policy/rule applicability (for example eligibility/coverage rules).

Entity selection:
- Claims-related -> CLAIMS
- Policy-related -> POLICIES
- Plan/coverage/product-related -> PLANS
- Application/proposal-related -> APPLICATIONS
- Payment/premium transaction-related -> PAYMENTS

FILTER EXTRACTION RULES:

- If query contains ""rejected"" -> filters.status = ""Rejected"".
- If query contains ""approved"" -> filters.status = ""Approved"".
- If query contains ""pending"" -> filters.status = ""Submitted"".

- If query contains ""theft"" -> filters.claimType = ""Theft"".
- If query contains ""damage"" -> filters.claimType = ""Damage"".
- If query contains ""third party"" -> filters.claimType = ""ThirdParty"".

- If no filter is present -> use empty string """".
- dateFrom/dateTo: extract if explicit dates/ranges are present, else """".

STRICT RULE:
- NEVER ignore filters if keywords exist.
- Always populate filters when detected.

Example:
- ""count rejected claims"" -> filters.status = ""Rejected"", aggregation = ""COUNT"".

Output constraints:
- Always include all fields.
- Use empty string """" for non-applicable string fields.
- Use ""NONE"" for non-applicable aggregation.
- Use limit as a non-negative integer.
- Ensure valid JSON only.

USER_QUERY: {query}";
        }

        private static async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            try
            {
                var credential = await GoogleCredential.GetApplicationDefaultAsync(cancellationToken);
                if (credential.IsCreateScopedRequired)
                {
                    credential = credential.CreateScoped(CloudPlatformScope);
                }

                if (credential.UnderlyingCredential is not ITokenAccess tokenAccess)
                {
                    return null;
                }

                var token = await tokenAccess.GetAccessTokenForRequestAsync(null, cancellationToken);
                return string.IsNullOrWhiteSpace(token) ? null : token;
            }
            catch
            {
                return null;
            }
        }

        private static string? ResolveProjectId()
        {
            var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
            if (!string.IsNullOrWhiteSpace(projectId))
            {
                return projectId;
            }

            projectId = Environment.GetEnvironmentVariable("GCP_PROJECT");
            if (!string.IsNullOrWhiteSpace(projectId))
            {
                return projectId;
            }

            projectId = Environment.GetEnvironmentVariable("GCLOUD_PROJECT");
            return string.IsNullOrWhiteSpace(projectId) ? null : projectId;
        }

        private static string? ExtractModelText(JsonElement root)
        {
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content))
            {
                return null;
            }

            if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0)
            {
                return null;
            }

            var firstPart = parts[0];
            if (!firstPart.TryGetProperty("text", out var textElement))
            {
                return null;
            }

            var text = textElement.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static string? ExtractJsonPayload(string text)
        {
            var cleaned = text.Trim();

            if (cleaned.StartsWith("```") && cleaned.EndsWith("```"))
            {
                var firstNewLine = cleaned.IndexOf('\n');
                if (firstNewLine >= 0)
                {
                    cleaned = cleaned[(firstNewLine + 1)..].Trim();
                }

                if (cleaned.EndsWith("```"))
                {
                    cleaned = cleaned[..^3].Trim();
                }
            }

            var start = cleaned.IndexOf('{');
            var end = cleaned.LastIndexOf('}');
            if (start < 0 || end < start)
            {
                return null;
            }

            return cleaned.Substring(start, end - start + 1);
        }
    }
}
