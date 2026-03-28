using System.Threading;
using System.Threading.Tasks;
using VIMS.Application.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IChatService
    {
        Task<string> AnswerQueryAsync(
            string query,
            IReadOnlyList<ChatHistoryItemDTO>? history = null,
            CancellationToken cancellationToken = default);
    }
}
