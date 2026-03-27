using System.Threading;
using System.Threading.Tasks;

namespace VIMS.Application.Interfaces.Services
{
    public interface IEmbeddingService
    {
        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    }
}
