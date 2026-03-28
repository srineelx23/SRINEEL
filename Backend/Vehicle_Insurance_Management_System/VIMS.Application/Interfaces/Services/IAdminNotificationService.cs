using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IAdminNotificationService
    {
        Task<IReadOnlyList<NotificationContextDto>> GetRecentNotificationsAsync(int take = 100, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<NotificationContextDto>> GetNotificationsByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    }
}