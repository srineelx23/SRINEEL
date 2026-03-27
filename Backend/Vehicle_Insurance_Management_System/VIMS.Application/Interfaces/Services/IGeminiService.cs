using System.Threading;
using System.Threading.Tasks;

namespace VIMS.Application.Interfaces.Services
{
    public interface IGeminiService
    {
        Task<string> GenerateAnswerAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
