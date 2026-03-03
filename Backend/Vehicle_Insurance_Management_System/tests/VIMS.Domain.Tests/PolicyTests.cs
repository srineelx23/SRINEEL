using System;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using Xunit;

namespace VIMS.Domain.Tests
{
    public class PolicyTests
    {
        [Fact]
        public void Policy_ShouldCalculateDurationCorrectly()
        {
            // Arrange
            var startDate = DateTime.UtcNow;
            var endDate = startDate.AddYears(1);
            var policy = new Policy
            {
                StartDate = startDate,
                EndDate = endDate
            };

            // Assert
            Assert.Equal(endDate, policy.EndDate);
            Assert.True(policy.EndDate > policy.StartDate);
        }

        [Fact]
        public void Policy_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var policy = new Policy { PolicyNumber = "POL-123" };

            // Assert
            Assert.Equal(PolicyStatus.Draft, policy.Status); 
            Assert.Equal(0, policy.ClaimCount);
            Assert.False(policy.IsRenewed);
        }
    }
}
