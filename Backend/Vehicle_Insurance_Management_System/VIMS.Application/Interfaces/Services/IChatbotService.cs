using VIMS.Application.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IChatbotService
    {
        Task<ChatbotResponseDTO> AskAsync(int userId, string role, string query, CancellationToken cancellationToken = default);
    }
}
