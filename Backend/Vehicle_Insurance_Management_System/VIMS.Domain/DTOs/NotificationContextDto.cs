namespace VIMS.Domain.DTOs
{
    public class NotificationContextDto
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? EntityName { get; set; }
        public string? EntityId { get; set; }
    }
}