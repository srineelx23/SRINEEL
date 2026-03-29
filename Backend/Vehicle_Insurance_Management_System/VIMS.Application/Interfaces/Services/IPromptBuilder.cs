using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IPromptBuilder
    {
        string Build(string question, ContextDataDto contextData, string precomputedAnalysisJson, IReadOnlyList<string>? history = null);
    }
}
