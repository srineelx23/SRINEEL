using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using Xunit;

namespace VIMS.Domain.Tests
{
    public class ClaimsTests
    {
        [Fact]
        public void Claims_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var claim = new Claims
            {
                ClaimNumber = "CLM-2026-001",
                Status = ClaimStatus.Submitted,
                claimType = ClaimType.Damage
            };

            // Assert
            Assert.Equal("CLM-2026-001", claim.ClaimNumber);
            Assert.Equal(ClaimStatus.Submitted, claim.Status);
            Assert.Equal(ClaimType.Damage, claim.claimType);
        }

        [Fact]
        public void Claims_ShouldHandleApprovedAmount()
        {
            // Arrange
            var claim = new Claims
            {
                ApprovedAmount = 2500.50m,
                DecisionType = "Partial"
            };

            // Assert
            Assert.Equal(2500.50m, claim.ApprovedAmount);
            Assert.Equal("Partial", claim.DecisionType);
        }

        [Fact]
        public void Claims_ShouldInitializeWithNullableOfficer()
        {
            // Arrange & Act
            var claim = new Claims();

            // Assert
            Assert.Null(claim.ClaimsOfficerId);
            Assert.Null(claim.ApprovedAmount);
        }
    }
}
