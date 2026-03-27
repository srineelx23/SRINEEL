using System.Threading.Tasks;
using System.Collections.Generic;
using VIMS.Application.DTOs;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Services
{
    public interface IClaimsService
    {
        Task<string> SubmitClaimAsync(SubmitClaimDTO dto, int customerId);
        Task<string> DecideClaimAsync(int claimId, ApproveClaimDTO dto, int officerId, bool approve);
        Task<List<Claims>> GetAllClaimsAsync();
        Task<List<Claims>> GetClaimsByCustomerAsync(int customerId);

        // Methods previously handled by controller via repository directly
        Task<Claims?> GetClaimByIdAsync(int claimId);
        Task<List<Claims>> GetClaimsByOfficerIdAsync(int officerId);
        Task<ClaimBreakdownDTO> CalculateClaimBreakdownAsync(int claimId, ApproveClaimDTO dto);
        Task<ClaimsAnalysisResultDTO> AnalyzeClaimAsync(int claimId);
    }
}
