using System.Text.Json;
using Microsoft.Extensions.Logging;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;

namespace VIMS.Application.Services.AdminAI
{
    public class LlmRouterService : ILlmService
    {
        private readonly IGeminiLlmProvider _geminiProvider;
        private readonly IGroqLlmProvider _groqProvider;
        private readonly ILogger<LlmRouterService> _logger;

        public LlmRouterService(
            IGeminiLlmProvider geminiProvider,
            IGroqLlmProvider groqProvider,
            ILogger<LlmRouterService> logger)
        {
            _geminiProvider = geminiProvider;
            _groqProvider = groqProvider;
            _logger = logger;
        }

        public string LastProvider { get; private set; } = "NONE";

        public async Task<LlmResponseDto> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            try
            {
                var geminiRaw = await _geminiProvider.GenerateAsync(prompt, cancellationToken);
                var parsed = ParseAndValidate(geminiRaw);
                LastProvider = _geminiProvider.ProviderName;
                return parsed;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning(ex, "Gemini hit HTTP 429. Switching to Groq fallback.");
                try
                {
                    var groqRaw = await _groqProvider.GenerateAsync(prompt, cancellationToken);
                    var parsed = ParseAndValidate(groqRaw);
                    LastProvider = _groqProvider.ProviderName;
                    return parsed;
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Groq fallback failed after Gemini 429.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini failed or returned invalid JSON.");
            }

            LastProvider = "NONE";
            return new LlmResponseDto
            {
                Answer = "Insufficient data to answer.",
                Reasoning = "Gemini was unavailable or returned invalid output, and fallback generation was unavailable.",
                RulesApplied = new List<string>(),
                DataUsed = new List<string>(),
                Confidence = "LOW"
            };
        }

        private static LlmResponseDto ParseAndValidate(string rawResponse)
        {
            var json = ExtractJson(rawResponse);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("LLM response is not a JSON object.");
            }

            var root = doc.RootElement;
            var result = new LlmResponseDto
            {
                Answer = GetFlexibleString(root, "answer"),
                Reasoning = GetFlexibleString(root, "reasoning"),
                Confidence = NormalizeConfidence(GetFlexibleString(root, "confidence")),
                RulesApplied = GetStringList(root, "rulesApplied"),
                DataUsed = GetStringList(root, "dataUsed")
            };

            if (string.IsNullOrWhiteSpace(result.Answer) || string.IsNullOrWhiteSpace(result.Reasoning))
            {
                throw new InvalidOperationException("LLM response missing required fields.");
            }

            result.RulesApplied ??= new List<string>();
            result.DataUsed ??= new List<string>();
            return result;
        }

        private static string GetFlexibleString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var value))
            {
                return string.Empty;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Array => string.Join(
                    " ",
                    value.EnumerateArray()
                        .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!.Trim())),
                JsonValueKind.Object => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => value.GetRawText(),
                _ => string.Empty
            };
        }

        private static List<string> GetStringList(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var value))
            {
                return new List<string>();
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray()
                    .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!.Trim())
                    .ToList();
            }

            var single = GetFlexibleString(root, propertyName);
            return string.IsNullOrWhiteSpace(single) ? new List<string>() : new List<string> { single };
        }

        private static string ExtractJson(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                throw new InvalidOperationException("LLM response is empty.");
            }

            var trimmed = response.Trim();
            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                throw new InvalidOperationException("No JSON object found in LLM response.");
            }

            return trimmed[start..(end + 1)];
        }

        private static string NormalizeConfidence(string? confidence)
        {
            var normalized = (confidence ?? string.Empty).Trim().ToUpperInvariant();
            return normalized is "HIGH" or "MEDIUM" or "LOW" ? normalized : "LOW";
        }
    }
}
