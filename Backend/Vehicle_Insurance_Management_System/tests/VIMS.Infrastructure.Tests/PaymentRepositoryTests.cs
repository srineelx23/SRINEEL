using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using VIMS.Infrastructure.Persistence;
using VIMS.Infrastructure.Repositories;
using Xunit;

namespace VIMS.Infrastructure.Tests
{
    public class PaymentRepositoryTests
    {
        private DbContextOptions<VehicleInsuranceContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<VehicleInsuranceContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task AddAsync_ShouldAddPayment()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            var repository = new PaymentRepository(context);
            var payment = new Payment { Amount = 1500, PolicyId = 1, Status = PaymentStatus.Paid };

            // Act
            await repository.AddAsync(payment);

            // Assert
            var savedPayment = await context.Payments.FirstOrDefaultAsync(p => p.Amount == 1500);
            Assert.NotNull(savedPayment);
            Assert.Equal(PaymentStatus.Paid, savedPayment.Status);
        }

        [Fact]
        public async Task HasUnpaidAsync_ShouldReturnTrue_WhenUnpaidExists()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            context.Payments.Add(new Payment { PolicyId = 1, Status = PaymentStatus.Pending });
            context.SaveChanges();
            var repository = new PaymentRepository(context);

            // Act
            var result = await repository.HasUnpaidAsync(1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task HasUnpaidAsync_ShouldReturnFalse_WhenAllPaid()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            context.Payments.Add(new Payment { PolicyId = 1, Status = PaymentStatus.Paid });
            context.SaveChanges();
            var repository = new PaymentRepository(context);

            // Act
            var result = await repository.HasUnpaidAsync(1);

            // Assert
            Assert.False(result);
        }
    }
}
