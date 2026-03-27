using System;
using System.Threading;
using System.Threading.Tasks;
using VIMS.Application.Interfaces.Services;

namespace VIMS.Application.Services
{
    public class ChatService : IChatService
    {
        private readonly ISafetyService _safetyService;
        private readonly IRAGService _ragService;
        private readonly IGeminiService _geminiService;

        public ChatService(ISafetyService safetyService, IRAGService ragService, IGeminiService geminiService)
        {
            _safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
            _ragService = ragService ?? throw new ArgumentNullException(nameof(ragService));
            _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
        }

        public async Task<string> AnswerQueryAsync(string query, CancellationToken cancellationToken = default)
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

            // 3. Else -> Retrieve top 3 chunks using RAGService
            var contextChunks = await _ragService.RetrieveAsync(query, cancellationToken);
            var contextString = string.Join("\n\n", contextChunks);

            // Build prompt with context
            var prompt = $@"
You are a vehicle insurance assistant.

STRICT RULES:
- Answer ONLY from provided context
- No external knowledge
- No sensitive data
- If not found → say ""I don’t have that information""

Context:
{contextString}

User Question:
{query}
";

            // Call GeminiService
            var response = await _geminiService.GenerateAnswerAsync(prompt, cancellationToken);
            
            return string.IsNullOrWhiteSpace(response) ? "I don't have that information." : response.Trim();
        }
    }
}
