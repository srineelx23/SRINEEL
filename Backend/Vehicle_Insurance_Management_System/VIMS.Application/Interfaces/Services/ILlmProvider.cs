namespace VIMS.Application.Interfaces.Services
{
    public interface ILlmProvider
    {
        string ProviderName { get; }
        Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
