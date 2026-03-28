namespace VIMS.Domain.DTOs
{
    public class UserContextDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? ReferralCode { get; set; }
        public int? ReferredByUserId { get; set; }
        public bool HasUsedReferral { get; set; }
    }
}
