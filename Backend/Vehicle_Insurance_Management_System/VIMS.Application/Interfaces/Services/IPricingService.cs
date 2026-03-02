using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Services
{
    public interface IPricingService
    {
        public PricingResultDTO CalculateAnnualPremium(
             CalculateQuoteDTO dto,
             PolicyPlan plan,
             bool isRenewal);
        // Expose IDV calculation so other services (e.g., claims) can use the same logic
        public decimal CalculateIDV(decimal invoiceAmount, int manufactureYear);
    }
}
