using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Services
{
    public interface IAuditService
    {
        Task LogActionAsync(string action, string category, string details, string? entityName = null, string? entityId = null);
        
        // Overload for cases where User info needs to be passed explicitly (e.g. during login before identity is set)
        Task LogActionWithUserAsync(string action, string category, string details, int? userId, string email, string role, string? entityName = null, string? entityId = null);
        Task<List<AuditLog>> GetAuditLogsAsync();
    }
}
