using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using Xunit;

namespace VIMS.Domain.Tests
{
    public class UserTests
    {
        [Fact]
        public void User_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var user = new User
            {
                UserId = 1,
                FullName = "Ramesh Kumar",
                Email = "ramesh@example.com",
                Role = UserRole.Customer,
                IsActive = true
            };

            // Assert
            Assert.Equal(1, user.UserId);
            Assert.Equal("Ramesh Kumar", user.FullName);
            Assert.Equal("ramesh@example.com", user.Email);
            Assert.Equal(UserRole.Customer, user.Role);
            Assert.True(user.IsActive);
        }
    }
}
