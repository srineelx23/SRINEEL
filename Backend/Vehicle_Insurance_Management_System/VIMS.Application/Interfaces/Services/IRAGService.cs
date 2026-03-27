using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VIMS.Application.Interfaces.Services
{
    public interface IRAGService
    {
        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<List<string>> RetrieveAsync(string query, CancellationToken cancellationToken = default);
    }
}
