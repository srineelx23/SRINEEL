using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VIMS.Application.Interfaces.Services;
using VIMS.Application.Settings;

namespace VIMS.Application.Services
{
    public class GroqService : IGroqService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";

        public GroqService(IHttpClientFactory httpClientFactory, IOptions<GroqSettings> settings)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiKey = settings.Value.ApiKey;
        }

        public async Task<string> SummarizeTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "No content available to summarize.";

            // Truncate text if it's too long to avoid token limits (very basic)
            if (text.Length > 6000) text = text.Substring(0, 6000);

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "system", content = "You are a professional insurance claim assistant. Summarize the claim document in 3-4 bullet points. IMPORTANT: Provide ONLY the bullet points. Start directly with 'Incident Details'. Do NOT include any introduction like 'Here are the points' or conclusion. Format: **[Category]**: [Details]" },
                    new { role = "user", content = text }
                },
                temperature = 0.5,
                max_tokens = 500
            };

            var request = new HttpRequestMessage(HttpMethod.Post, GroqEndpoint);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return "Failed to generate summary via Groq.";

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);
            return jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "Unknown summary";
        }

        public async Task<string> AnalyzeRiskAsync(string text, string claimContext)
        {
             // Similar to summary but focused on finding mismatches or suspicious details
             // This is used as an aid, local logic performs specific date/number matching too.
             var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "system", content = "Identify any discrepancies in the provided text regarding this insurance claim. Context: " + claimContext + ". Look for vehicle number mismatches or date inconsistencies." },
                    new { role = "user", content = text }
                },
                temperature = 0.3
            };

            var request = new HttpRequestMessage(HttpMethod.Post, GroqEndpoint);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return "Risk analysis failed.";

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);
            return jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "Analyzed without specific flags";
        }
    }
}
