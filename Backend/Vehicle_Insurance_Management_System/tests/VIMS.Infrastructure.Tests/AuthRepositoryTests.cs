using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using VIMS.Infrastructure.Persistence;
using VIMS.Infrastructure.Repositories;
using Xunit;

namespace VIMS.Infrastructure.Tests
{
    public class AuthRepositoryTests
    {
        private DbContextOptions<VehicleInsuranceContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<VehicleInsuranceContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task RegisterCustomerAsync_ShouldAddUserToDatabase()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            var repository = new AuthRepository(context);
            var user = new User { FullName = "Anjali Singh", Email = "anjali@example.com", Role = UserRole.Customer };

            // Act
            await repository.RegisterCustomerAsync(user);

            // Assert
            var savedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "anjali@example.com");
            Assert.NotNull(savedUser);
            Assert.Equal("Anjali Singh", savedUser.FullName);
        }

        [Fact]
        public async Task UserExistsAsync_ShouldReturnUser_WhenUserExists()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            context.Users.Add(new User { FullName = "John Doe", Email = "john@example.com", Role = UserRole.Customer });
            context.SaveChanges();
            var repository = new AuthRepository(context);

            // Act
            var result = await repository.UserExistsAsync("john@example.com");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("John Doe", result.FullName);
        }

        [Fact]
        public async Task UserExistsAsync_ShouldReturnNull_WhenUserDoesNotExist()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            var repository = new AuthRepository(context);

            // Act
            var result = await repository.UserExistsAsync("nonexistent@example.com");

            // Assert
            Assert.Null(result);
        }
    }
}
