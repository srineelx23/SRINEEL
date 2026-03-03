using VIMS.Domain.Entities;
using Xunit;

namespace VIMS.Domain.Tests
{
    public class VehicleTests
    {
        [Fact]
        public void Vehicle_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var vehicle = new Vehicle
            {
                VehicleId = 1,
                RegistrationNumber = "KA01AB1234",
                Make = "Honda",
                Model = "City",
                Year = 2022,
                FuelType = "Petrol",
                VehicleType = "Four-Wheeler"
            };

            // Assert
            Assert.Equal(1, vehicle.VehicleId);
            Assert.Equal("KA01AB1234", vehicle.RegistrationNumber);
            Assert.Equal("Honda", vehicle.Make);
            Assert.Equal("City", vehicle.Model);
            Assert.Equal(2022, vehicle.Year);
            Assert.Equal("Petrol", vehicle.FuelType);
            Assert.Equal("Four-Wheeler", vehicle.VehicleType);
        }
    }
}
