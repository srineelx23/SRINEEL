using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;

namespace VIMS.Infrastructure.Services.RAG
{
    public class InMemoryVectorStoreService : IVectorStoreService
    {
        private readonly List<(RagChunkDTO Chunk, float[] Embedding)> _entries = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _entries.Clear();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task UpsertAsync(IReadOnlyList<(RagChunkDTO Chunk, float[] Embedding)> entries, CancellationToken cancellationToken = default)
        {
            if (entries.Count == 0)
            {
                return;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _entries.AddRange(entries);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<(RagChunkDTO Chunk, float[] Embedding)>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                return _entries
                    .Select(e =>
                    {
                        var clonedChunk = new RagChunkDTO
                        {
                            Type = e.Chunk.Type,
                            Source = e.Chunk.Source,
                            SourceType = e.Chunk.SourceType,
                            SourceId = e.Chunk.SourceId,
                            Text = e.Chunk.Text,
                            Similarity = e.Chunk.Similarity
                        };

                        return (clonedChunk, e.Embedding);
                    })
                    .ToList();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
