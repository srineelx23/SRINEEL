using System;

namespace VIMS.Domain.Enums
{
    public enum NotificationType
    {
        PolicyApproved = 0,
        PolicyRejected = 1,
        PremiumPaymentDue = 2,
        ClaimSubmitted = 3,
        PolicyExpiring = 4,
        ClaimApproved = 5,
        ClaimRejected = 6,
        PolicyRequestSubmitted = 7,
        PolicyTransferStatusChanged = 8,
        NewPolicyRequestAssigned = 9,
        NewClaimAssigned = 10
    }
}
