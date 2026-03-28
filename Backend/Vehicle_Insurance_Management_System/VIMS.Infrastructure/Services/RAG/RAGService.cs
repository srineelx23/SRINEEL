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

            var rawChunks = SplitIntoOptimalChunks(text, 400); 
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

        public async Task<List<string>> RetrieveAsync(string query, CancellationToken cancellationToken = default)
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
            var topMatches = vectorSearchService.GetTopMatches(queryVector, candidates, 3);

            var scoredChunks = topMatches
                .Select(c => c.Text)
                .ToList();

            return scoredChunks;
        }

        public Task<List<string>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            return RetrieveAsync(query, cancellationToken);
        }

        private List<string> SplitIntoOptimalChunks(string text, int targetSize)
        {
            var chunks = new List<string>();
            var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var currentChunk = new List<string>();
            var currentLength = 0;

            foreach (var word in words)
            {
                if (currentLength + word.Length + 1 > targetSize && currentChunk.Count > 0)
                {
                    chunks.Add(string.Join(" ", currentChunk));
                    currentChunk.Clear();
                    currentLength = 0;
                }

                currentChunk.Add(word);
                currentLength += word.Length + 1;
            }

            if (currentChunk.Count > 0)
            {
                chunks.Add(string.Join(" ", currentChunk));
            }

            return chunks;
        }

    }
}
