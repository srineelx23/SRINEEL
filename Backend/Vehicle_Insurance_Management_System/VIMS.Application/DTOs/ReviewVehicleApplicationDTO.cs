using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VIMS.Application.DTOs
{
    public class ReviewVehicleApplicationDTO
    {
        public bool Approved { get; set; }
        public string? RejectionReason { get; set; }
        public decimal InvoiceAmount { get; set; }
    }
}
