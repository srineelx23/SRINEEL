using System.Threading.Tasks;

namespace VIMS.Application.Interfaces.Services
{
    public interface IGroqService
    {
        Task<string> SummarizeTextAsync(string text);
        Task<string> AnalyzeRiskAsync(string text, string claimContext);
    }
}
