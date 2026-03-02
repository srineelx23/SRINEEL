using System;

namespace VIMS.Application.DTOs
{
    public class ApproveClaimDTO
    {
        // Common
        public decimal? RepairCost { get; set; }
        // For own damage: optional engine cost
        public decimal? EngineCost { get; set; }
        // For third party: invoice amount and manufacture year
        public decimal? InvoiceAmount { get; set; }
        public int? ManufactureYear { get; set; }
    }
}
