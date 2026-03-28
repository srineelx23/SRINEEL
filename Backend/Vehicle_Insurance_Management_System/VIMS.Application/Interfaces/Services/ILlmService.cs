using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface ILlmService
    {
        string LastProvider { get; }
        Task<LlmResponseDto> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
