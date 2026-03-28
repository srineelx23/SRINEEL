using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Services.AdminAI
{
    public class AdminNotificationService : IAdminNotificationService
    {
        private readonly VehicleInsuranceContext _context;

        public AdminNotificationService(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<NotificationContextDto>> GetRecentNotificationsAsync(int take = 100, CancellationToken cancellationToken = default)
        {
            var safeTake = take <= 0 ? 100 : take;
            return await _context.Notifications
                .AsNoTracking()
                .OrderByDescending(n => n.CreatedAt)
                .Take(safeTake)
                .Select(Map())
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<NotificationContextDto>> GetNotificationsByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(Map())
                .ToListAsync(cancellationToken);
        }

        private static System.Linq.Expressions.Expression<Func<VIMS.Domain.Entities.Notification, NotificationContextDto>> Map()
        {
            return n => new NotificationContextDto
            {
                NotificationId = n.NotificationId,
                UserId = n.UserId,
                UserName = n.User != null ? n.User.FullName : string.Empty,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type.ToString(),
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                EntityName = n.EntityName,
                EntityId = n.EntityId
            };
        }
    }
}