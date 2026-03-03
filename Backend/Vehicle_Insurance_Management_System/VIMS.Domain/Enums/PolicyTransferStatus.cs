namespace VIMS.Domain.Enums
{
    public enum PolicyTransferStatus
    {
        PendingRecipientAcceptance = 0,
        RejectedByRecipient = 1,
        PendingAgentApproval = 2,
        Completed = 3,
        Cancelled = 4
    }
}
