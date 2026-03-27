using System.Threading;
using System.Threading.Tasks;
using VIMS.Application.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface ISafetyService
    {
        Task<SafetyClassificationDTO> ClassifyQueryAsync(string query, CancellationToken cancellationToken = default);
    }
}
