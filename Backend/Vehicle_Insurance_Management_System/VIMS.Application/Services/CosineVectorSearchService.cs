using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;

namespace VIMS.Application.Services
{
    public class CosineVectorSearchService : IVectorSearchService
    {
        public IReadOnlyList<RagChunkDTO> GetTopMatches(
            float[] queryEmbedding,
            IReadOnlyList<(RagChunkDTO Chunk, float[] Embedding)> candidates,
            int topK)
        {
            if (queryEmbedding.Length == 0 || candidates.Count == 0 || topK <= 0)
            {
                return Array.Empty<RagChunkDTO>();
            }

            var scored = new List<RagChunkDTO>(candidates.Count);
            foreach (var candidate in candidates)
            {
                var similarity = CosineSimilarity(queryEmbedding, candidate.Embedding);
                candidate.Chunk.Similarity = similarity;
                scored.Add(candidate.Chunk);
            }

            return scored
                .OrderByDescending(c => c.Similarity)
                .Take(topK)
                .ToList();
        }

        private static float CosineSimilarity(float[] left, float[] right)
        {
            if (left.Length == 0 || right.Length == 0 || left.Length != right.Length)
            {
                return 0f;
            }

            double dot = 0;
            double leftNorm = 0;
            double rightNorm = 0;

            for (var i = 0; i < left.Length; i++)
            {
                dot += left[i] * right[i];
                leftNorm += left[i] * left[i];
                rightNorm += right[i] * right[i];
            }

            if (leftNorm == 0 || rightNorm == 0)
            {
                return 0f;
            }

            return (float)(dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)));
        }
    }
}
