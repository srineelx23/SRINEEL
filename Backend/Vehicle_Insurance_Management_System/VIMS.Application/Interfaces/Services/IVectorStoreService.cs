using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VIMS.Application.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IVectorStoreService
    {
        Task ClearAsync(CancellationToken cancellationToken = default);
        Task UpsertAsync(IReadOnlyList<(RagChunkDTO Chunk, float[] Embedding)> entries, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<(RagChunkDTO Chunk, float[] Embedding)>> GetAllAsync(CancellationToken cancellationToken = default);
    }
}
