using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VIMS.Application.DTOs
{
    public class PricingResultDTO
    {
        public decimal IDV { get; set; }
        public decimal Premium { get; set; }

        // Breakdown components
        public decimal TPComponent { get; set; }
        public decimal ODComponent { get; set; }
        public decimal BasePremium { get; set; }
        public decimal RiskLoadingAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
    }
}
