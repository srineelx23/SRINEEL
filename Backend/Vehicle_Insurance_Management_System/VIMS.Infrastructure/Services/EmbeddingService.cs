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
    public class EmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public EmbeddingService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<float>();
            }

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("Gemini API Key is not configured.");
            }

            var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:embedContent?key={_apiKey}";

            var requestBody = new
            {
                model = "models/gemini-embedding-001",
                content = new
                {
                    parts = new[] { new { text } }
                }
            };

            using var response = await _httpClient.PostAsJsonAsync(requestUri, requestBody, cancellationToken);
            
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var jsonDoc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (jsonDoc.RootElement.TryGetProperty("embedding", out var embeddingElement) &&
                embeddingElement.TryGetProperty("values", out var valuesElement))
            {
                var length = valuesElement.GetArrayLength();
                var result = new float[length];
                int i = 0;
                foreach (var value in valuesElement.EnumerateArray())
                {
                    result[i++] = value.GetSingle();
                }
                return result;
            }

            return Array.Empty<float>();
        }
    }
}
