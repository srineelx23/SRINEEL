using System.Collections.Generic;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IAuditLogRepository
    {
        Task AddAsync(AuditLog auditLog);
        Task<List<AuditLog>> GetAllAsync();
    }
}
