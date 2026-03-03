using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using VIMS.Infrastructure.Persistence;
using VIMS.Infrastructure.Repositories;
using Xunit;

namespace VIMS.Infrastructure.Tests
{
    public class PolicyRepositoryTests
    {
        private DbContextOptions<VehicleInsuranceContext> CreateNewContextOptions()
        {
            return new DbContextOptionsBuilder<VehicleInsuranceContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        [Fact]
        public async Task AddAsync_ShouldAddPolicyToDatabase()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            var repository = new PolicyRepository(context);
            var policy = new Policy { PolicyNumber = "POL-101", CustomerId = 1, VehicleId = 1 };

            // Act
            await repository.AddAsync(policy);

            // Assert
            var savedPolicy = await context.Policies.FirstOrDefaultAsync(p => p.PolicyNumber == "POL-101");
            Assert.NotNull(savedPolicy);
            Assert.Equal("POL-101", savedPolicy.PolicyNumber);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnPolicy_WhenExists()
        {
            // Arrange
            var options = CreateNewContextOptions();
            int savedPolicyId;
            using (var context = new VehicleInsuranceContext(options))
            {
                var user = new User { FullName = "Test User", Email = "test@vims.com", Role = UserRole.Customer };
                context.Users.Add(user);
                var plan = new PolicyPlan { PlanName = "Plan1", BasePremium = 1000 };
                context.PolicyPlans.Add(plan);
                context.SaveChanges();

                var app = new VehicleApplication { RegistrationNumber = "TESTREG", CustomerId = user.UserId, PlanId = plan.PlanId, Make = "Test", Model = "Test" };
                context.VehicleApplications.Add(app);
                context.SaveChanges();

                var vehicle = new Vehicle { RegistrationNumber = "TESTREG", CustomerId = user.UserId, Make = "Test", Model = "Test", VehicleApplicationId = app.VehicleApplicationId };
                context.Vehicles.Add(vehicle);
                context.SaveChanges();

                var policy = new Policy { PolicyNumber = "POL-102", CustomerId = user.UserId, VehicleId = vehicle.VehicleId, PlanId = plan.PlanId };
                context.Policies.Add(policy);
                context.SaveChanges();
                savedPolicyId = policy.PolicyId;
            }

            using (var context2 = new VehicleInsuranceContext(options))
            {
                var repository = new PolicyRepository(context2);

                // Act
                var result = await repository.GetByIdAsync(savedPolicyId);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("POL-102", result.PolicyNumber);
            }
        }

        [Fact]
        public async Task UpdateAsync_ShouldModifyPolicy()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            var policy = new Policy { PolicyNumber = "POL-103", Status = PolicyStatus.Draft, CustomerId = 1, VehicleId = 1 };
            context.Policies.Add(policy);
            context.SaveChanges();
            var repository = new PolicyRepository(context);

            // Act
            policy.Status = PolicyStatus.Active;
            await repository.UpdateAsync(policy);

            // Assert
            var updatedPolicy = await context.Policies.FindAsync(policy.PolicyId);
            Assert.Equal(PolicyStatus.Active, updatedPolicy.Status);
        }

        [Fact]
        public async Task AddAndExpireAsync_ShouldHandleTransactionally()
        {
            // Arrange
            var options = CreateNewContextOptions();
            using var context = new VehicleInsuranceContext(options);
            var oldPolicy = new Policy { PolicyNumber = "POL-OLD", Status = PolicyStatus.Active, CustomerId = 1, VehicleId = 1 };
            context.Policies.Add(oldPolicy);
            context.SaveChanges();
            var repository = new PolicyRepository(context);
            var newPolicy = new Policy { PolicyNumber = "POL-NEW", Status = PolicyStatus.Active, CustomerId = 1, VehicleId = 1 };

            // Act
            await repository.AddAndExpireAsync(newPolicy, oldPolicy);

            // Assert
            var expiredPolicy = await context.Policies.FindAsync(oldPolicy.PolicyId);
            Assert.Equal(PolicyStatus.Expired, expiredPolicy.Status);
            Assert.True(expiredPolicy.IsRenewed);
            var savedNewPolicy = await context.Policies.FirstOrDefaultAsync(p => p.PolicyNumber == "POL-NEW");
            Assert.NotNull(savedNewPolicy);
        }
    }
}
