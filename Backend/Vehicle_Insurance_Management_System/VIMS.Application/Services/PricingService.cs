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

            // Get base components from premium calculation
            var (tpComp, odComp, riskL) = CalculatePremiumComponents(
                plan,
                idv,
                dto.VehicleType,
                dto.KilometersDriven
            );

            decimal annualPremium = tpComp + odComp + riskL;
            decimal preDiscountPremium = annualPremium;

            // Commitment Discount
            decimal commitmentDiscountRate = dto.PolicyYears switch
            {
                2 => 0.05m,
                3 => 0.08m,
                >= 4 => 0.10m,
                _ => 0m
            };

            decimal discount = annualPremium * commitmentDiscountRate;
            annualPremium -= discount;

            // Loyalty Discount
            if (isRenewal)
            {
                decimal loyaltyDiscount = annualPremium * 0.05m;
                discount += loyaltyDiscount;
                annualPremium -= loyaltyDiscount;
            }

            // GST / Tax (Assume 18% standard for insurance in India)
            decimal taxRate = 0.18m;
            decimal taxAmount = annualPremium * taxRate;
            decimal finalPremium = annualPremium + taxAmount;

            return new PricingResultDTO
            {
                IDV = Math.Round(idv, 0),
                Premium = Math.Round(finalPremium, 0),
                TPComponent = Math.Round(tpComp, 2),
                ODComponent = Math.Round(odComp, 2),
                BasePremium = Math.Round(tpComp + odComp, 2),
                RiskLoadingAmount = Math.Round(riskL, 2),
                DiscountAmount = Math.Round(discount, 2),
                TaxAmount = Math.Round(taxAmount, 2)
            };
        }

        private (decimal tp, decimal od, decimal risk) CalculatePremiumComponents(
            PolicyPlan plan,
            decimal idv,
            string vehicleType,
            int kilometersDriven)
        {
            string policyType = plan.PolicyType?.ToLower() ?? "";
            
            decimal tpPart = 0;
            decimal odPart = 0;
            decimal riskPart = 0;

            if (policyType == "thirdparty")
            {
                tpPart = plan.BasePremium;

                if (vehicleType == "HeavyVehicle")
                    tpPart *= 1.10m;

                if (vehicleType.Contains("EV"))
                    tpPart *= 0.95m;
            }
            else
            {
                tpPart = plan.BasePremium * 0.40m;
                odPart = idv * 0.022m;

                decimal riskFactor = 1.0m;
                if (kilometersDriven > 50000)
                    riskFactor += 0.05m;

                if (policyType == "zerodepreciation")
                    riskFactor *= 1.20m;
                
                decimal combined = tpPart + odPart;
                riskPart = (combined * riskFactor) - combined;
            }

            return (tpPart, odPart, riskPart);
        }

        // Backward compatibility if needed by interface
        public PricingResultDTO CalculateAnnualPremium(CalculateQuoteDTO dto, PolicyPlan plan) 
            => CalculateAnnualPremium(dto, plan, false);
    }
}
