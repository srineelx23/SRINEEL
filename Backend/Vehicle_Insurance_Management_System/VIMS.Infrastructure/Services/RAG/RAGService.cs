using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;

namespace VIMS.Infrastructure.Services.RAG
{
    public class RAGService : IRAGService
    {
        private const int ChunkTargetSize = 180;
        private const int ChunkMinSize = 120;
        private const int ChunkOverlapSize = 30;
        private const float MinSimilarityThreshold = 0.65f;
        private const int CandidatePoolSize = 12;
        private const int MaxSelectedRules = 3;

        private readonly IServiceProvider _serviceProvider;
        private readonly IVectorStoreService _vectorStoreService;
        private readonly ILogger<RAGService> _logger;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public RAGService(IServiceProvider serviceProvider, IVectorStoreService vectorStoreService, ILogger<RAGService> logger)
        {
            _serviceProvider = serviceProvider;
            _vectorStoreService = vectorStoreService;
            _logger = logger;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized) return;

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_isInitialized) return;

                var dataPath = Path.Combine(AppContext.BaseDirectory, "Data");
                if (!Directory.Exists(dataPath))
                {
                    _logger.LogWarning("RAG data folder not found in output directory: {DataPath}", dataPath);
                    _isInitialized = true;
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

                await _vectorStoreService.ClearAsync(cancellationToken);

                // 1. Process .txt files
                var txtFiles = Directory.GetFiles(dataPath, "*.txt");
                foreach (var file in txtFiles)
                {
                    try
                    {
                        var text = await File.ReadAllTextAsync(file, cancellationToken);
                        await IndexTextAsync(text, embeddingService, cancellationToken, "txt", Path.GetFileName(file));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "RAG indexing skipped for file {FileName}", Path.GetFileName(file));
                    }
                }

                // 2. Process .json files
                var jsonFiles = Directory.GetFiles(dataPath, "*.json");
                foreach (var file in jsonFiles)
                {
                    try
                    {
                        var text = await File.ReadAllTextAsync(file, cancellationToken);
                        // For RAG we index the whole JSON structure as a context block for now
                        // In a bigger system we might iterate properties
                        await IndexTextAsync(text, embeddingService, cancellationToken, "json", Path.GetFileName(file));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "RAG indexing skipped for file {FileName}", Path.GetFileName(file));
                    }
                }

                // 3. Process .jsonl files
                var jsonlFiles = Directory.GetFiles(dataPath, "*.jsonl");
                foreach (var file in jsonlFiles)
                {
                    try
                    {
                        var lines = await File.ReadAllLinesAsync(file, cancellationToken);
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            try
                            {
                                using var doc = JsonDocument.Parse(line);
                                if (doc.RootElement.TryGetProperty("content", out var contentElem))
                                {
                                    await IndexTextAsync(contentElem.GetString() ?? "", embeddingService, cancellationToken, "jsonl", Path.GetFileName(file));
                                }
                                else
                                {
                                    await IndexTextAsync(line, embeddingService, cancellationToken, "jsonl", Path.GetFileName(file));
                                }
                            }
                            catch
                            {
                                await IndexTextAsync(line, embeddingService, cancellationToken, "jsonl", Path.GetFileName(file));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "RAG indexing skipped for file {FileName}", Path.GetFileName(file));
                    }
                }

                _isInitialized = true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task IndexTextAsync(
            string text,
            IEmbeddingService embeddingService,
            CancellationToken ct,
            string sourceType,
            string sourceId)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var ruleType = InferRuleType(sourceId, text);
            var rawChunks = SplitIntoOptimalChunks(text, ChunkTargetSize);
            var entries = new List<(RagChunkDTO Chunk, float[] Embedding)>();

            foreach (var chunkText in rawChunks)
            {
                try
                {
                    var vector = await embeddingService.GenerateEmbeddingAsync(chunkText, ct);
                    if (vector != null && vector.Length > 0)
                    {
                        entries.Add((
                            new RagChunkDTO
                            {
                                Type = ruleType,
                                Source = sourceId,
                                SourceType = sourceType,
                                SourceId = sourceId,
                                Text = chunkText,
                                Similarity = 0f
                            },
                            vector));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "RAG chunk skipped for source {SourceType}:{SourceId}", sourceType, sourceId);
                }
            }

            if (entries.Count > 0)
            {
                await _vectorStoreService.UpsertAsync(entries, ct);
            }
        }

        public async Task<List<string>> RetrieveAsync(string query, string intentType, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                try
                {
                    await InitializeAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RAG initialization failed. Continuing without rule retrieval.");
                    return new List<string>();
                }
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<string>();
            }

            using var scope = _serviceProvider.CreateScope();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            var vectorSearchService = scope.ServiceProvider.GetRequiredService<IVectorSearchService>();

            float[] queryVector;
            try
            {
                queryVector = await embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RAG query embedding failed. Returning empty rule set.");
                return new List<string>();
            }
            if (queryVector == null || queryVector.Length == 0)
            {
                return new List<string>();
            }

            var candidates = await _vectorStoreService.GetAllAsync(cancellationToken);
            var normalizedIntentType = NormalizeIntentType(intentType);
            var intentFilteredCandidates = FilterByIntentType(candidates, normalizedIntentType);

            if (intentFilteredCandidates.Count == 0)
            {
                _logger.LogInformation(
                    "RAG retrieval found no chunks for intentType={IntentType}. Query={Query}",
                    normalizedIntentType,
                    query);
                return new List<string>();
            }

            var topMatches = vectorSearchService.GetTopMatches(queryVector, intentFilteredCandidates, CandidatePoolSize);

            foreach (var match in topMatches)
            {
                _logger.LogInformation(
                    "RAG candidate score={Score:F3} type={Type} source={Source}",
                    match.Similarity,
                    match.Type,
                    match.Source);
            }

            var highRelevance = topMatches
                .Where(c => c.Similarity >= MinSimilarityThreshold)
                .ToList();

            var deduplicated = RemoveDuplicatesAndOverlaps(highRelevance);

            var limited = deduplicated
                .Take(MaxSelectedRules)
                .ToList();

            foreach (var selected in limited)
            {
                _logger.LogInformation(
                    "RAG selected rule score={Score:F3} type={Type} source={Source} text={Text}",
                    selected.Similarity,
                    selected.Type,
                    selected.Source,
                    TruncateForLog(selected.Text, 160));
            }

            var scoredChunks = limited
                .Select(c => c.Text)
                .ToList();

            return scoredChunks;
        }

        public Task<List<string>> SearchAsync(string query, string intentType, CancellationToken cancellationToken = default)
        {
            return RetrieveAsync(query, intentType, cancellationToken);
        }

        private List<string> SplitIntoOptimalChunks(string text, int targetSize)
        {
            var chunks = new List<string>();
            var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return chunks;
            }

            var sentences = normalized
                .Split(new[] { ". ", "! ", "? ", "; " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            if (sentences.Count == 0)
            {
                sentences.Add(normalized);
            }

            var current = string.Empty;
            foreach (var sentence in sentences)
            {
                var candidate = string.IsNullOrEmpty(current) ? sentence : $"{current}. {sentence}";
                if (candidate.Length <= targetSize)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(current))
                {
                    chunks.Add(current.Trim());
                }

                if (sentence.Length > targetSize)
                {
                    chunks.AddRange(SplitLongSentence(sentence, targetSize));
                    current = string.Empty;
                }
                else
                {
                    current = sentence;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                chunks.Add(current.Trim());
            }

            chunks = ApplyChunkOverlap(chunks, ChunkOverlapSize)
                .Select(c => c.Trim())
                .Where(c => c.Length >= Math.Min(ChunkMinSize, targetSize))
                .ToList();

            if (chunks.Count == 0 && normalized.Length > 0)
            {
                chunks.Add(normalized.Length <= targetSize ? normalized : normalized[..targetSize]);
            }

            return chunks;
        }

        private static List<string> SplitLongSentence(string sentence, int targetSize)
        {
            var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var parts = new List<string>();
            var current = new List<string>();
            var len = 0;

            foreach (var word in words)
            {
                if (len + word.Length + 1 > targetSize && current.Count > 0)
                {
                    parts.Add(string.Join(" ", current));
                    current.Clear();
                    len = 0;
                }

                current.Add(word);
                len += word.Length + 1;
            }

            if (current.Count > 0)
            {
                parts.Add(string.Join(" ", current));
            }

            return parts;
        }

        private static IReadOnlyList<string> ApplyChunkOverlap(IReadOnlyList<string> chunks, int overlapChars)
        {
            if (chunks.Count <= 1 || overlapChars <= 0)
            {
                return chunks;
            }

            var overlapped = new List<string>(chunks.Count);
            for (var i = 0; i < chunks.Count; i++)
            {
                var current = chunks[i];
                if (i == 0)
                {
                    overlapped.Add(current);
                    continue;
                }

                var previous = chunks[i - 1];
                var prefix = previous.Length <= overlapChars
                    ? previous
                    : previous[^overlapChars..];

                overlapped.Add($"{prefix} {current}".Trim());
            }

            return overlapped;
        }

        private static string InferRuleType(string sourceId, string text)
        {
            var source = (sourceId ?? string.Empty).ToLowerInvariant();
            var content = (text ?? string.Empty).ToLowerInvariant();

            if (source.Contains("claim") || content.Contains("claim")) return "CLAIM";
            if (source.Contains("referral") || content.Contains("referral") || content.Contains("referrer") || content.Contains("referee")) return "REFERRAL";
            if (source.Contains("payment") || content.Contains("payment") || content.Contains("premium") || content.Contains("invoice")) return "PAYMENT";
            if (source.Contains("policy") || content.Contains("policy") || content.Contains("coverage") || content.Contains("eligible")) return "POLICY";
            return "POLICY";
        }

        private static string NormalizeIntentType(string intentType)
        {
            if (string.IsNullOrWhiteSpace(intentType)) return "MIXED";

            var normalized = intentType.Trim().ToUpperInvariant();
            return normalized switch
            {
                "CLAIM" => "CLAIM",
                "POLICY" => "POLICY",
                "REFERRAL" => "REFERRAL",
                "PAYMENT" => "PAYMENT",
                _ => "MIXED"
            };
        }

        private static IReadOnlyList<(RagChunkDTO Chunk, float[] Embedding)> FilterByIntentType(
            IReadOnlyList<(RagChunkDTO Chunk, float[] Embedding)> candidates,
            string intentType)
        {
            if (intentType == "MIXED")
            {
                return candidates;
            }

            return candidates
                .Where(c => string.Equals(c.Chunk.Type, intentType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static List<RagChunkDTO> RemoveDuplicatesAndOverlaps(IReadOnlyList<RagChunkDTO> matches)
        {
            var result = new List<RagChunkDTO>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var match in matches.OrderByDescending(m => m.Similarity))
            {
                var normalized = NormalizeText(match.Text);
                if (normalized.Length == 0 || seen.Contains(normalized))
                {
                    continue;
                }

                var isOverlap = result.Any(existing => IsOverlapping(existing.Text, match.Text));
                if (isOverlap)
                {
                    continue;
                }

                seen.Add(normalized);
                result.Add(match);
            }

            return result;
        }

        private static bool IsOverlapping(string left, string right)
        {
            var a = NormalizeText(left);
            var b = NormalizeText(right);
            if (a.Length == 0 || b.Length == 0)
            {
                return false;
            }

            if (a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var leftTokens = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rightTokens = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (leftTokens.Count == 0 || rightTokens.Count == 0)
            {
                return false;
            }

            var intersection = leftTokens.Intersect(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
            var union = leftTokens.Union(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
            var jaccard = union == 0 ? 0d : (double)intersection / union;
            return jaccard >= 0.75;
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return string.Join(" ", text
                .Trim()
                .ToLowerInvariant()
                .Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string TruncateForLog(string input, int max)
        {
            if (string.IsNullOrWhiteSpace(input) || input.Length <= max)
            {
                return input;
            }

            return input[..max] + "...";
        }

    }
}
