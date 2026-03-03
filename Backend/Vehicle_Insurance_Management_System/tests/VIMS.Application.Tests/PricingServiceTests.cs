using VIMS.Application.DTOs;
using VIMS.Application.Services;
using VIMS.Domain.Entities;
using Xunit;

namespace VIMS.Application.Tests
{
    public class PricingServiceTests
    {
        private readonly PricingService _pricingService;

        public PricingServiceTests()
        {
            _pricingService = new PricingService();
        }

        [Fact]
        public void CalculateIDV_ShouldApplyDepreciation_WhenVehicleAgeIs3Years()
        {
            // Arrange
            decimal invoiceAmount = 1000000;
            int manufactureYear = DateTime.UtcNow.Year - 3; // 3 years old

            // Act
            decimal idvResult = _pricingService.CalculateIDV(invoiceAmount, manufactureYear);

            // Assert
            // 3 years depreciation according to service is 0.30m
            // Expect idv = 1000000 - (1000000 * 0.30) = 700000
            Assert.Equal(700000, idvResult);
        }

        [Fact]
        public void CalculateIDV_ShouldApplyDepreciation_WhenVehicleAgeIsNew()
        {
            // Arrange
            decimal invoiceAmount = 1000000;
            int manufactureYear = DateTime.UtcNow.Year; // 0 years old

            // Act
            decimal idvResult = _pricingService.CalculateIDV(invoiceAmount, manufactureYear);

            // Assert
            // 0 years depreciation is 0.05m
            // Expect idv = 1000000 - (1000000 * 0.05) = 950000
            Assert.Equal(950000, idvResult);
        }

        [Fact]
        public void CalculateAnnualPremium_ShouldApplyCommitmentDiscount_WhenPolicyYearsIs3()
        {
            // Arrange
            var dto = new CalculateQuoteDTO
            {
                InvoiceAmount = 1000000,
                ManufactureYear = DateTime.UtcNow.Year,
                PolicyYears = 3,
                VehicleType = "Four-Wheeler"
            };
            var plan = new PolicyPlan
            {
                BasePremium = 10000,
                PolicyType = "Comprehensive"
            };

            // Act
            var result = _pricingService.CalculateAnnualPremium(dto, plan, false);

            // Assert
            // IDV = 0.95 * 10M = 950K
            // TP Component = 10K * 0.40 = 4000
            // OD Component = 950K * 0.022 = 20900
            // Base Premium = 4000 + 20900 = 24900
            // Commitment Discount (3 years) = 0.08m
            // Final = 24900 - (24900 * 0.08) = 24900 - 1992 = 22908
            Assert.Equal(22908, result.Premium);
        }

        [Fact]
        public void CalculateAnnualPremium_ShouldApplyLoyaltyDiscount_WhenIsRenewalIsTrue()
        {
            // Arrange
            var dto = new CalculateQuoteDTO
            {
                InvoiceAmount = 1000000,
                ManufactureYear = DateTime.UtcNow.Year,
                PolicyYears = 1,
                VehicleType = "Four-Wheeler"
            };
            var plan = new PolicyPlan
            {
                BasePremium = 10000,
                PolicyType = "Comprehensive"
            };

            // Act
            var result = _pricingService.CalculateAnnualPremium(dto, plan, true);

            // Assert
            // IDV = 0.95 * 10M = 950K
            // TP Component = 10K * 0.40 = 4000
            // OD Component = 950K * 0.022 = 20900
            // Base Premium = 4000 + 20900 = 24900
            // Loyalty Discount = 0.05m
            // Final = 24900 - (24900 * 0.05) = 24900 - 1245 = 23655
            Assert.Equal(23655, result.Premium);
        }
    }
}
