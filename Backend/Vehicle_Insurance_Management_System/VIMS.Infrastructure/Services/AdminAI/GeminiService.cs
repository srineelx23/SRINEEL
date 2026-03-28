using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VIMS.Application.Interfaces.Services;
using VIMS.Application.Settings;

namespace VIMS.Infrastructure.Services.AdminAI
{
    public class GeminiService : IGeminiLlmProvider
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiSettings _settings;

        public GeminiService(HttpClient httpClient, IOptions<GeminiSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public string ProviderName => "Gemini";

        public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new InvalidOperationException("Gemini API key is not configured.");
            }

            var model = string.IsNullOrWhiteSpace(_settings.ChatModel) ? "gemini-2.5-flash" : _settings.ChatModel;
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_settings.ApiKey}";

            var body = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    responseMimeType = "application/json"
                }
            };

            using var response = await _httpClient.PostAsJsonAsync(endpoint, body, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new HttpRequestException("Gemini rate limit exceeded (429).", null, response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorPayload = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"Gemini returned {(int)response.StatusCode} ({response.StatusCode}). Body: {errorPayload}",
                    null,
                    response.StatusCode);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var text = document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text ?? string.Empty;
        }
    }
}
