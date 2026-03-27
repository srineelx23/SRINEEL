using System;

namespace VIMS.Infrastructure.Services.RAG
{
    public class DocumentChunk
    {
        public string Text { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
