using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IPromptBuilder
    {
        string Build(string question, ContextDataDto contextData, IReadOnlyList<string>? history = null);
    }
}
