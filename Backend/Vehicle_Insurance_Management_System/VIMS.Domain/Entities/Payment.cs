using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Enums;

namespace VIMS.Domain.Entities
{
    public class Payment
    {
        [Key]   
        public int PaymentId { get; set; }
        public int PolicyId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public PaymentStatus Status { get; set; }
        public string? TransactionReference { get; set; }
        public PaymentMethod PaymentMethod { get; set; }

        // Navigation
        public Policy Policy { get; set; } = null!;
    }
}
