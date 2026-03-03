using System;

namespace VIMS.Domain.Entities
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }
        public string Action { get; set; } = string.Empty; // e.g., Login, UserCreated, PolicyApproved
        public string Category { get; set; } = string.Empty; // Auth, User, Policy, Claim, Payment, Query
        public int? UserId { get; set; } // Who performed the action (can be null for some system actions)
        public string Email { get; set; } = string.Empty; // Email for quick lookup
        public string Role { get; set; } = string.Empty; // Role of the person at that time
        public string? EntityName { get; set; } // Affected entity type (User, Policy, etc.)
        public string? EntityId { get; set; } // ID of affected entity
        public string? Details { get; set; } // Human-readable details
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? IPAddress { get; set; }
    }
}
