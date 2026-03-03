using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VIMS.Domain.Entities;
using VIMS.Infrastructure.Persistence;
using VIMS.Infrastructure.Repositories;
using Xunit;

namespace VIMS.Infrastructure.Tests
{
    public class VehicleRepositoryTests
    {
        private DbContextOptions<VehicleInsuranceContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<VehicleInsuranceContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task AddAsync_ShouldAddVehicle()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            var repository = new VehicleRepository(context);
            var vehicle = new Vehicle { RegistrationNumber = "KA01AB1234", CustomerId = 1 };

            // Act
            await repository.AddAsync(vehicle);

            // Assert
            var savedVehicle = await context.Vehicles.FirstOrDefaultAsync(v => v.RegistrationNumber == "KA01AB1234");
            Assert.NotNull(savedVehicle);
            Assert.Equal("KA01AB1234", savedVehicle.RegistrationNumber);
        }

        [Fact]
        public async Task GetByRegistrationNumberAsync_ShouldReturnVehicle_WhenExists()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            context.Vehicles.Add(new Vehicle { RegistrationNumber = "MH02CD5678", CustomerId = 1 });
            context.SaveChanges();
            var repository = new VehicleRepository(context);

            // Act
            var result = await repository.GetByRegistrationNumberAsync("MH02CD5678");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("MH02CD5678", result.RegistrationNumber);
        }

        [Fact]
        public void Update_ShouldModifyState()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            var vehicle = new Vehicle { RegistrationNumber = "TN03EF9012", CustomerId = 1, Model = "Swift" };
            context.Vehicles.Add(vehicle);
            context.SaveChanges();
            var repository = new VehicleRepository(context);

            // Act
            vehicle.Model = "Swift Dzire";
            repository.Update(vehicle);
            context.SaveChanges();

            // Assert
            var updatedVehicle = context.Vehicles.FirstOrDefault(v => v.RegistrationNumber == "TN03EF9012");
            Assert.Equal("Swift Dzire", updatedVehicle.Model);
        }

        [Fact]
        public async Task GetByRegistrationNumberAsync_ShouldReturnNull_WhenNotExists()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            var repository = new VehicleRepository(context);

            // Act
            var result = await repository.GetByRegistrationNumberAsync("XX00YY1111");

            // Assert
            Assert.Null(result);
        }
    }
}
