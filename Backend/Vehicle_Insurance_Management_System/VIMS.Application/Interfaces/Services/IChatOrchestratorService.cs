using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IChatOrchestratorService
    {
        Task<ChatResponseDto> ProcessAdminQueryAsync(ChatRequestDto request, CancellationToken cancellationToken = default);
    }
}
