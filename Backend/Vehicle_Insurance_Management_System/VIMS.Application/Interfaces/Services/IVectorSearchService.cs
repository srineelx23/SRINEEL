using VIMS.Application.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IVectorSearchService
    {
        IReadOnlyList<RagChunkDTO> GetTopMatches(float[] queryEmbedding, IReadOnlyList<(RagChunkDTO Chunk, float[] Embedding)> candidates, int topK);
    }
}
