using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using VIMS.Application.Interfaces.Services;

namespace VIMS.Infrastructure.Services
{
    public class HybridAIService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _geminiApiKey;
        private readonly string _groqApiKey;
        private static DateTime? _fallbackUntil;
        private readonly string _geminiEndpoint;
        private const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";

        public HybridAIService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _geminiApiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
            _groqApiKey = configuration["Groq:ApiKey"] ?? string.Empty;
            var chatModel = configuration["Gemini:ChatModel"] ?? "gemini-2.5-flash";
            _geminiEndpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{chatModel}:generateContent?key={_geminiApiKey}";
        }

        public async Task<string> GenerateAnswerAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return string.Empty;

            // 1. Check if we are currently in fallback mode
            if (_fallbackUntil.HasValue && DateTime.Now < _fallbackUntil.Value)
            {
                return await CallGroqAsync(prompt, cancellationToken);
            }

            // 2. Try Gemini
            try
            {
                if (string.IsNullOrWhiteSpace(_geminiApiKey)) throw new InvalidOperationException("Gemini Key missing.");

                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };

                using var response = await _httpClient.PostAsJsonAsync(_geminiEndpoint, requestBody, cancellationToken);
                
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || 
                    (int)response.StatusCode >= 500)
                {
                    // Error/Quota reached -> Switch to Groq for 1 hour
                    _fallbackUntil = DateTime.Now.AddHours(1);
                    return await CallGroqAsync(prompt, cancellationToken);
                }

                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var jsonDoc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) && 
                    candidates.GetArrayLength() > 0)
                {
                    var text = candidates[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();
                    
                    return text ?? string.Empty;
                }
            }
            catch
            {
                // Uncaught Exception -> Switch to Groq
                _fallbackUntil = DateTime.Now.AddHours(1);
                return await CallGroqAsync(prompt, cancellationToken);
            }

            return string.Empty;
        }

        private async Task<string> CallGroqAsync(string prompt, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_groqApiKey)) return "I'm sorry, I'm currently unable to process your request as all AI models are unavailable.";

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.5
            };

            var request = new HttpRequestMessage(HttpMethod.Post, GroqEndpoint);
            request.Headers.Add("Authorization", $"Bearer {_groqApiKey}");
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try 
            {
                using var response = await _httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) return "AI assistance currently disconnected.";

                var responseContent = await response.Content.ReadAsStringAsync(ct);
                using var jsonDoc = JsonDocument.Parse(responseContent);
                var text = jsonDoc.RootElement
                                  .GetProperty("choices")[0]
                                  .GetProperty("message")
                                  .GetProperty("content")
                                  .GetString();
                
                return $"{text}\n\n[Note: Answered by Llama via Groq]";
            }
            catch
            {
                return "AI assistance currently unavailable.";
            }
        }
    }
}
