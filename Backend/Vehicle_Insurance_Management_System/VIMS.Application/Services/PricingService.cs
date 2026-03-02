using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;

namespace VIMS.Application.Services
{
    public class PricingService : IPricingService
    {
        public decimal CalculateIDV(decimal invoiceAmount, int manufactureYear)
        {
            int age = DateTime.UtcNow.Year - manufactureYear;

            decimal depreciation = age switch
            {
                <= 0 => 0.05m,
                1 => 0.15m,
                2 => 0.20m,
                3 => 0.30m,
                4 => 0.40m,
                5 => 0.50m,
                _ => 0.60m
            };

            return invoiceAmount - (invoiceAmount * depreciation);
        }

        public PricingResultDTO CalculateAnnualPremium(
            CalculateQuoteDTO dto,
            PolicyPlan plan,
            bool isRenewal)
        {
            decimal idv = CalculateIDV(dto.InvoiceAmount, dto.ManufactureYear);

            decimal annualPremium = CalculatePremium(
                plan,
                idv,
                dto.VehicleType,
                dto.KilometersDriven
            );

            // Commitment Discount
            decimal commitmentDiscount = dto.PolicyYears switch
            {
                2 => 0.05m,
                3 => 0.08m,
                >= 4 => 0.10m,
                _ => 0m
            };

            annualPremium -= annualPremium * commitmentDiscount;

            // Loyalty Discount
            if (isRenewal)
                annualPremium -= annualPremium * 0.05m;

            return new PricingResultDTO
            {
                IDV = Math.Round(idv, 0),
                Premium = Math.Round(annualPremium, 0)
            };
        }

        private decimal CalculatePremium(
            PolicyPlan plan,
            decimal idv,
            string vehicleType,
            int kilometersDriven)
        {
            string policyType = plan.PolicyType?.ToLower() ?? "";

            decimal annualPremium;

            if (policyType == "thirdparty")
            {
                annualPremium = plan.BasePremium;

                if (vehicleType == "HeavyVehicle")
                    annualPremium *= 1.10m;

                if (vehicleType.Contains("EV"))
                    annualPremium *= 0.95m;
            }
            else
            {
                decimal tpComponent = plan.BasePremium * 0.40m;
                decimal odComponent = idv * 0.022m;

                decimal riskLoading = 1.0m;

                if (kilometersDriven > 50000)
                    riskLoading += 0.05m;

                annualPremium = (tpComponent + odComponent) * riskLoading;

                if (policyType == "zerodepreciation")
                    annualPremium *= 1.20m;
            }

            return annualPremium;
        }
    }
}
