namespace VIMS.Application.Interfaces.Services
{
    public interface IHybridRuleEngineService
    {
        Task<string> ExecuteAsync(string query, CancellationToken cancellationToken = default);
    }
}
