using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IClaimsRepository
    {
        Task<Claims> AddAsync(Claims claim);
        Task AddDocumentAsync(ClaimDocument doc);
        Task<Claims?> GetByIdAsync(int claimId);
        Task<List<Claims>> GetByOfficerIdAsync(int officerId);
        Task<List<Claims>> GetByCustomerIdsAsync(IReadOnlyCollection<int> customerIds);
        Task UpdateAsync(Claims claim);
        Task<List<Claims>> GetAllAsync();
        Task<bool> ExistsActiveClaimForPolicyAsync(int policyId);
        Task<List<Claims>> GetByCustomerIdAsync(int customerId);
        Task<bool> HasAnyClaimsAsync(int policyId);
    }
}
