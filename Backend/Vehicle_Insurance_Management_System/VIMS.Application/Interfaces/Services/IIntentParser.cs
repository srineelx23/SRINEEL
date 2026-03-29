using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IIntentParser
    {
        IntentResultDto Parse(string question, IReadOnlyList<string>? history = null, ContextMemoryDto? sessionMemory = null);
    }
}
