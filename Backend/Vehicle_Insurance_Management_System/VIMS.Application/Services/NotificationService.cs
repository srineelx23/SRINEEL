using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IPolicyRepository _policyRepository;

        public NotificationService(
            INotificationRepository notificationRepository,
            IPushNotificationService pushNotificationService,
            IPolicyRepository policyRepository)
        {
            _notificationRepository = notificationRepository;
            _pushNotificationService = pushNotificationService;
            _policyRepository = policyRepository;
        }

        public async Task CreateNotificationAsync(int userId, string title, string message, NotificationType type, string? entityName = null, string? entityId = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                EntityName = entityName,
                EntityId = entityId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _notificationRepository.AddAsync(notification);
            
            // Real-time Push
            await _pushNotificationService.NotifyUserAsync(userId, notification);
            
            // Also notify admins for all actions as per requirements
            await NotifyAdminsForActionAsync(notification);
        }

        public async Task NotifyAdminsAsync(string title, string message, NotificationType type, string? entityName = null, string? entityId = null)
        {
            var adminIds = await _notificationRepository.GetAdminUserIdsAsync();
            foreach (var adminId in adminIds)
            {
                var notification = new Notification
                {
                    UserId = adminId,
                    Title = title,
                    Message = message,
                    Type = type,
                    EntityName = entityName,
                    EntityId = entityId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _notificationRepository.AddAsync(notification);
                await _pushNotificationService.NotifyUserAsync(adminId, notification);
            }
        }

        private async Task NotifyAdminsForActionAsync(Notification originalNotification)
        {
            // Requirement 4.i: Admin gets "Above mentioned all actions"
            var adminIds = await _notificationRepository.GetAdminUserIdsAsync();
            foreach (var adminId in adminIds)
            {
                if (adminId == originalNotification.UserId) continue; // Already sent if user is admin

                var adminNotification = new Notification
                {
                    UserId = adminId,
                    Title = $"[SYSTEM] {originalNotification.Title}",
                    Message = originalNotification.Message,
                    Type = originalNotification.Type,
                    EntityName = originalNotification.EntityName,
                    EntityId = originalNotification.EntityId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _notificationRepository.AddAsync(adminNotification);
                await _pushNotificationService.NotifyUserAsync(adminId, adminNotification);
            }
        }

        public async Task<IEnumerable<Notification>> GetUserNotificationsAsync(int userId)
        {
            return await _notificationRepository.GetByUserIdAsync(userId);
        }

        public async Task<IEnumerable<Notification>> GetUnreadUserNotificationsAsync(int userId)
        {
            return await _notificationRepository.GetUnreadByUserIdAsync(userId);
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _notificationRepository.GetUnreadCountByUserIdAsync(userId);
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            await _notificationRepository.MarkAsReadAsync(notificationId);
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            await _notificationRepository.MarkAllAsReadAsync(userId);
        }

        public async Task DeleteNotificationAsync(int id)
        {
            await _notificationRepository.DeleteAsync(id);
        }

        public async Task CheckAndNotifyExpiringPoliciesAsync()
        {
            // Notify customer before 45 days, 30 days, 15 days, 7 days, 1 day.
            var thresholds = new[] { 45, 30, 15, 7, 1 };
            
            foreach (var days in thresholds)
            {
                var targetDate = DateTime.UtcNow.Date.AddDays(days);
                var policies = await _policyRepository.GetExpiringPoliciesAsync(targetDate);
                
                foreach (var policy in policies)
                {
                    await CreateNotificationAsync(
                        policy.CustomerId,
                        "Policy Expiring Soon",
                        $"Your policy {policy.PolicyNumber} will expire in {days} days on {policy.EndDate:d}. Please renew soon.",
                        NotificationType.PolicyExpiring,
                        "Policy",
                        policy.PolicyId.ToString()
                    );
                }
            }
        }
    }
}
