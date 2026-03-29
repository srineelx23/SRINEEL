using System.Text.Json;
using System.Net;
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
            Exception? geminiFailure = null;
            Exception? groqFailure = null;

            try
            {
                var parsed = await TryProviderWithRetryAsync(
                    _geminiProvider.ProviderName,
                    ct => _geminiProvider.GenerateAsync(prompt, ct),
                    maxAttempts: 3,
                    cancellationToken);
                LastProvider = _geminiProvider.ProviderName;
                return parsed;
            }
            catch (Exception ex)
            {
                geminiFailure = ex;
                _logger.LogWarning(ex, "Gemini failed. Switching to Groq fallback.");
            }

            try
            {
                var parsed = await TryProviderWithRetryAsync(
                    _groqProvider.ProviderName,
                    ct => _groqProvider.GenerateAsync(prompt, ct),
                    maxAttempts: 3,
                    cancellationToken);
                LastProvider = _groqProvider.ProviderName;
                return parsed;
            }
            catch (Exception ex)
            {
                groqFailure = ex;
                _logger.LogError(ex, "Groq fallback failed after Gemini failure.");
            }

            var geminiRateLimited = IsRateLimited(geminiFailure);
            var groqRateLimited = IsRateLimited(groqFailure);
            var reasoning = geminiRateLimited && groqRateLimited
                ? "Both Gemini and Groq are temporarily rate-limited (HTTP 429). Please retry shortly."
                : "Gemini was unavailable or returned invalid output, and fallback generation was unavailable.";

            LastProvider = "NONE";
            return new LlmResponseDto
            {
                Answer = "Insufficient data to answer.",
                Reasoning = reasoning,
                RulesApplied = new List<string>(),
                DataUsed = new List<string>(),
                Confidence = "LOW"
            };
        }

        private async Task<LlmResponseDto> TryProviderWithRetryAsync(
            string providerName,
            Func<CancellationToken, Task<string>> generateRaw,
            int maxAttempts,
            CancellationToken cancellationToken)
        {
            Exception? lastFailure = null;
            var invalidJsonRetries = 0;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var raw = await generateRaw(cancellationToken);
                    return ParseAndValidate(raw);
                }
                catch (Exception ex) when (IsInvalidJsonOrSchemaError(ex))
                {
                    lastFailure = ex;
                    _logger.LogWarning(
                        ex,
                        "{Provider} returned invalid JSON/schema on attempt {Attempt}. Error snippet: {Snippet}",
                        providerName,
                        attempt,
                        ExtractLogSnippet(ex));

                    if (invalidJsonRetries < 1 && attempt < maxAttempts)
                    {
                        invalidJsonRetries++;
                        _logger.LogWarning("{Provider} retrying once for invalid JSON response.", providerName);
                        continue;
                    }

                    break;
                }
                catch (Exception ex) when (attempt < maxAttempts && IsRetryable(ex))
                {
                    lastFailure = ex;
                    var delay = CalculateBackoffDelay(attempt);
                    _logger.LogWarning(
                        ex,
                        "{Provider} attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelayMs}ms.",
                        providerName,
                        attempt,
                        maxAttempts,
                        (int)delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    lastFailure = ex;
                    break;
                }
            }

            throw lastFailure ?? new InvalidOperationException($"{providerName} failed without exception details.");
        }

        private static bool IsRetryable(Exception ex)
        {
            if (ex is TaskCanceledException)
            {
                return true;
            }

            if (ex is HttpRequestException httpEx)
            {
                return httpEx.StatusCode == HttpStatusCode.TooManyRequests ||
                       (httpEx.StatusCode.HasValue && (int)httpEx.StatusCode.Value >= 500);
            }

            if (ex is InvalidOperationException invalidOpEx)
            {
                var message = invalidOpEx.Message ?? string.Empty;
                return message.Contains("No JSON object found", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool IsInvalidJsonOrSchemaError(Exception ex)
        {
            if (ex is JsonException)
            {
                return true;
            }

            if (ex is InvalidOperationException invalidOpEx)
            {
                var message = invalidOpEx.Message ?? string.Empty;
                return message.Contains("No JSON object found", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("JSON schema validation failed", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("LLM response is not a JSON object", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool IsRateLimited(Exception? ex)
        {
            return ex is HttpRequestException httpEx && httpEx.StatusCode == HttpStatusCode.TooManyRequests;
        }

        private static TimeSpan CalculateBackoffDelay(int attempt)
        {
            var ms = 800 * attempt * attempt;
            return TimeSpan.FromMilliseconds(ms);
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

            if (!root.TryGetProperty("answer", out var answerElement))
            {
                throw new InvalidOperationException("JSON schema validation failed: required field 'answer' missing.");
            }

            if (!root.TryGetProperty("reasoning", out var reasoningElement))
            {
                throw new InvalidOperationException("JSON schema validation failed: required field 'reasoning' missing.");
            }

            if (!root.TryGetProperty("confidence", out var confidenceElement))
            {
                throw new InvalidOperationException("JSON schema validation failed: required field 'confidence' missing.");
            }

            var result = new LlmResponseDto
            {
                Answer = GetFlexibleString(answerElement),
                Reasoning = GetFlexibleString(reasoningElement),
                Confidence = NormalizeConfidence(GetFlexibleString(confidenceElement)),
                RulesApplied = GetStringList(root, "rulesApplied"),
                DataUsed = GetStringList(root, "dataUsed")
            };

            if (string.IsNullOrWhiteSpace(result.Answer) || string.IsNullOrWhiteSpace(result.Reasoning))
            {
                throw new InvalidOperationException("JSON schema validation failed: required field values are empty.");
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

            return GetFlexibleString(value);
        }

        private static string GetFlexibleString(JsonElement value)
        {

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
            if (TryExtractFirstJsonObject(trimmed, out var json))
            {
                return json;
            }

            throw new InvalidOperationException("No JSON object found in LLM response.");
        }

        private static bool TryExtractFirstJsonObject(string input, out string json)
        {
            json = string.Empty;

            for (var start = 0; start < input.Length; start++)
            {
                if (input[start] != '{')
                {
                    continue;
                }

                var candidate = TryExtractBalancedObject(input, start);
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(candidate);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        json = candidate;
                        return true;
                    }
                }
                catch (JsonException)
                {
                    // Keep scanning when non-JSON text surrounds malformed candidates.
                }
            }

            return false;
        }

        private static string? TryExtractBalancedObject(string input, int startIndex)
        {
            var depth = 0;
            var inString = false;
            var escaped = false;

            for (var i = startIndex; i < input.Length; i++)
            {
                var ch = input[i];

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (ch == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (ch == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    continue;
                }

                if (ch == '{')
                {
                    depth++;
                    continue;
                }

                if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return input[startIndex..(i + 1)];
                    }
                }
            }

            return null;
        }

        private static string ExtractLogSnippet(Exception ex)
        {
            var message = ex.Message ?? string.Empty;
            if (message.Length <= 220)
            {
                return message;
            }

            return message[..220] + "...";
        }

        private static string NormalizeConfidence(string? confidence)
        {
            var normalized = (confidence ?? string.Empty).Trim().ToUpperInvariant();
            return normalized is "HIGH" or "MEDIUM" or "LOW" ? normalized : "LOW";
        }
    }
}
