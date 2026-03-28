using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VIMS.Application.Interfaces.Services;
using VIMS.Application.Settings;

namespace VIMS.Infrastructure.Services.AdminAI
{
    public class GroqService : IGroqLlmProvider
    {
        private const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";
        private readonly HttpClient _httpClient;
        private readonly GroqSettings _settings;

        public GroqService(HttpClient httpClient, IOptions<GroqSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public string ProviderName => "Groq";

        public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new InvalidOperationException("Groq API key is not configured.");
            }

            var model = string.IsNullOrWhiteSpace(_settings.Model) ? "llama-3.3-70b-versatile" : _settings.Model;
            var body = new
            {
                model,
                temperature = 0.1,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var text = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return text ?? string.Empty;
        }
    }
}
