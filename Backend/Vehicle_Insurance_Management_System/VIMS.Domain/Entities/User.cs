using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Enums;

namespace VIMS.Domain.Entities
{
    public class User
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? ReferralCode { get; set; }
        public int? ReferredByUserId { get; set; }
        public bool HasUsedReferral { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string? SecurityQuestion { get; set; }
        public string? SecurityAnswerHash { get; set; }
        //public string? LicenseNumber { get; set; }
        //public string? AgentCode { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsFirstLogin { get; set; } = false;

        // Navigation
        public ICollection<Vehicle>? Vehicles { get; set; }
        public ICollection<Policy>? CustomerPolicies { get; set; }
        public ICollection<Policy>? AgentPolicies { get; set; }
        public ICollection<Claims>? CustomerClaims { get; set; }
        public ICollection<Claims>? ClaimsHandled { get; set; }
        public Wallet? Wallet { get; set; }
        public ICollection<Referral>? ReferralsMade { get; set; }
        public ICollection<Referral>? ReferralsReceived { get; set; }
    }
}
