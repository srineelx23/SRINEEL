using System;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using Xunit;

namespace VIMS.Domain.Tests
{
    public class PolicyTransferTests
    {
        [Fact]
        public void PolicyTransfer_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var transfer = new PolicyTransfer
            {
                PolicyId = 1,
                SenderCustomerId = 10,
                RecipientCustomerId = 11,
                Status = PolicyTransferStatus.PendingRecipientAcceptance
            };

            // Assert
            Assert.Equal(1, transfer.PolicyId);
            Assert.Equal(10, transfer.SenderCustomerId);
            Assert.Equal(11, transfer.RecipientCustomerId);
            Assert.Equal(PolicyTransferStatus.PendingRecipientAcceptance, transfer.Status);
        }

        [Fact]
        public void PolicyTransfer_ShouldInitializeWithCreatedAt()
        {
            // Arrange & Act
            var transfer = new PolicyTransfer();

            // Assert
            Assert.True(transfer.CreatedAt <= DateTime.UtcNow);
            Assert.Equal(PolicyTransferStatus.PendingRecipientAcceptance, transfer.Status);
        }
    }

    public class VehicleApplicationTests
    {
        [Fact]
        public void VehicleApplication_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var app = new VehicleApplication
            {
                RegistrationNumber = "KA01AB1234",
                Make = "Honda",
                Model = "Civic",
                Year = 2021,
                FuelType = "Petrol",
                VehicleType = "Car",
                KilometersDriven = 15000,
                Status = VehicleApplicationStatus.UnderReview,
                IsTransfer = false
            };

            // Assert
            Assert.Equal("KA01AB1234", app.RegistrationNumber);
            Assert.Equal("Honda", app.Make);
            Assert.Equal("Civic", app.Model);
            Assert.Equal(2021, app.Year);
            Assert.Equal("Petrol", app.FuelType);
            Assert.Equal("Car", app.VehicleType);
            Assert.Equal(15000, app.KilometersDriven);
            Assert.Equal(VehicleApplicationStatus.UnderReview, app.Status);
        }

        [Fact]
        public void VehicleApplication_ShouldHandleTransferFlag()
        {
            // Arrange
            var app = new VehicleApplication
            {
                IsTransfer = true
            };

            // Assert
            Assert.True(app.IsTransfer);
        }
    }
}
