using System;
using VIMS.Domain.Enums;

namespace VIMS.Domain.Entities
{
    public class Notification
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; } // Receiver
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Link to associated entity for navigation (optional but helpful)
        public string? EntityName { get; set; } // e.g., Policy, Claim
        public string? EntityId { get; set; }

        // Navigation
        public User? User { get; set; }
    }
}
