using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Services
{
    public class AuditService : IAuditService
    {
        private readonly VehicleInsuranceContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(VehicleInsuranceContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogActionAsync(string action, string category, string details, string? entityName = null, string? entityId = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            int? userId = null;
            string email = "System";
            string role = "System";


            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int id))
                {
                    userId = id;
                }
                
                email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value ?? 
                        httpContext.User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
                
                role = httpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? 
                       httpContext.User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value ?? "User";
            }

            var auditLog = new AuditLog
            {
                Action = action,
                Category = category,
                UserId = userId,
                Email = email,
                Role = role,
                EntityName = entityName,
                EntityId = entityId,
                Details = details,
                Timestamp = DateTime.UtcNow,
            };

            await _context.AuditLogs.AddAsync(auditLog);
            await _context.SaveChangesAsync();
        }

        public async Task LogActionWithUserAsync(string action, string category, string details, int? userId, string email, string role, string? entityName = null, string? entityId = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;


            var auditLog = new AuditLog
            {
                Action = action,
                Category = category,
                UserId = userId,
                Email = email,
                Role = role,
                EntityName = entityName,
                EntityId = entityId,
                Details = details,
                Timestamp = DateTime.UtcNow,
            };

            await _context.AuditLogs.AddAsync(auditLog);
            await _context.SaveChangesAsync();
        }

        public async Task<List<AuditLog>> GetAuditLogsAsync()
        {
            return await _context.AuditLogs.OrderByDescending(x => x.Timestamp).ToListAsync();
        }
    }
}
