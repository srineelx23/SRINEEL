using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.Application.Interfaces.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(int userId, string title, string message, NotificationType type, string? entityName = null, string? entityId = null);
        Task NotifyAdminsAsync(string title, string message, NotificationType type, string? entityName = null, string? entityId = null);
        Task<IEnumerable<Notification>> GetUserNotificationsAsync(int userId);
        Task<IEnumerable<Notification>> GetUnreadUserNotificationsAsync(int userId);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync(int userId);
        Task DeleteNotificationAsync(int id);
        
        // System wide notification for expiration
        Task CheckAndNotifyExpiringPoliciesAsync();
    }
}
