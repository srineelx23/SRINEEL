using System;
using System.ComponentModel.DataAnnotations;
using VIMS.Domain.Enums;

namespace VIMS.Domain.Entities
{
    public class PolicyTransfer
    {
        [Key]
        public int PolicyTransferId { get; set; }

        // The policy being transferred
        public int PolicyId { get; set; }
        public Policy Policy { get; set; } = null!;

        // The customer initiating the transfer (seller)
        public int SenderCustomerId { get; set; }
        public User SenderCustomer { get; set; } = null!;

        // The customer receiving the transfer (buyer)
        public int RecipientCustomerId { get; set; }
        public User RecipientCustomer { get; set; } = null!;

        // The new VehicleApplication created when recipient uploads RC
        public int? NewVehicleApplicationId { get; set; }
        public VehicleApplication? NewVehicleApplication { get; set; }

        public PolicyTransferStatus Status { get; set; } = PolicyTransferStatus.PendingRecipientAcceptance;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? RejectionReason { get; set; }
    }
}
