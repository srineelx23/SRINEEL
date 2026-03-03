using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using Xunit;

namespace VIMS.Domain.Tests
{
    public class PolicyPlanTests
    {
        [Fact]
        public void PolicyPlan_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var plan = new PolicyPlan
            {
                PlanName = "Premium Comprehensive",
                BasePremium = 5000,
                Status = PlanStatus.Active
            };

            // Assert
            Assert.Equal("Premium Comprehensive", plan.PlanName);
            Assert.Equal(5000, plan.BasePremium);
            Assert.Equal(PlanStatus.Active, plan.Status);
        }

        [Fact]
        public void PolicyPlan_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var plan = new PolicyPlan();

            // Assert
            Assert.Equal(PlanStatus.Active, plan.Status);
            Assert.Empty(plan.PlanName);
        }

        [Fact]
        public void PolicyPlan_ShouldHandleCoverageFlags()
        {
            // Arrange
            var plan = new PolicyPlan
            {
                CoversTheft = true,
                CoversNaturalDisaster = false
            };

            // Assert
            Assert.True(plan.CoversTheft);
            Assert.False(plan.CoversNaturalDisaster);
        }
    }
}
