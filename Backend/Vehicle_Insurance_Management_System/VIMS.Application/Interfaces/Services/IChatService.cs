using System.Threading;
using System.Threading.Tasks;

namespace VIMS.Application.Interfaces.Services
{
    public interface IChatService
    {
        Task<string> AnswerQueryAsync(string query, CancellationToken cancellationToken = default);
    }
}
