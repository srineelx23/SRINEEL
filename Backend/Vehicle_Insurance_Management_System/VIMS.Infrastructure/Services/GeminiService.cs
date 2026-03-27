using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using VIMS.Application.Interfaces.Services;

namespace VIMS.Infrastructure.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        }

        public async Task<string> GenerateAnswerAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("Gemini API Key is not configured.");
            }

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = prompt } }
                    }
                }
            };

            using var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var jsonDoc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) && 
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) && 
                    parts.GetArrayLength() > 0)
                {
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var textElement))
                    {
                        return textElement.GetString() ?? string.Empty;
                    }
                }
            }

            return string.Empty;
        }
    }
}
