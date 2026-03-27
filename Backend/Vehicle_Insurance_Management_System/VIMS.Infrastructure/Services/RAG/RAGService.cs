using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VIMS.Application.Interfaces.Services;

namespace VIMS.Infrastructure.Services.RAG
{
    public class RAGService : IRAGService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly List<DocumentChunk> _chunks;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public RAGService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _chunks = new List<DocumentChunk>();
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
                    throw new DirectoryNotFoundException($"Data folder not found in output directory: {dataPath}");

                // Use robust DI scope handling
                using var scope = _serviceProvider.CreateScope();
                var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

                var files = Directory.GetFiles(dataPath, "*.txt");
                foreach (var file in files)
                {
                    var text = await File.ReadAllTextAsync(file, cancellationToken);
                    var rawChunks = SplitIntoOptimalChunks(text, 400); 

                    foreach (var chunkText in rawChunks)
                    {
                        var vector = await embeddingService.GenerateEmbeddingAsync(chunkText, cancellationToken);
                        if (vector != null && vector.Length > 0)
                        {
                            _chunks.Add(new DocumentChunk
                            {
                                Text = chunkText,
                                Embedding = vector
                            });
                        }
                    }
                }

                _isInitialized = true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<string>> RetrieveAsync(string query, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                await InitializeAsync(cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<string>();
            }

            using var scope = _serviceProvider.CreateScope();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

            var queryVector = await embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
            if (queryVector == null || queryVector.Length == 0)
            {
                return new List<string>();
            }

            var scoredChunks = _chunks
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Score = ComputeCosineSimilarity(queryVector, chunk.Embedding)
                })
                .OrderByDescending(x => x.Score)
                .Take(3)
                .Select(x => x.Chunk.Text)
                .ToList();

            return scoredChunks;
        }

        private List<string> SplitIntoOptimalChunks(string text, int targetSize)
        {
            var chunks = new List<string>();
            var words = text.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
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

        private float ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
                return 0f;

            float dotProduct = 0f;
            float magnitudeA = 0f;
            float magnitudeB = 0f;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            magnitudeA = (float)Math.Sqrt(magnitudeA);
            magnitudeB = (float)Math.Sqrt(magnitudeB);

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0f;

            return dotProduct / (magnitudeA * magnitudeB);
        }
    }
}
