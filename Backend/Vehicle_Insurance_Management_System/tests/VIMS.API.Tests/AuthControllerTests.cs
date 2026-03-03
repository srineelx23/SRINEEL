using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Threading.Tasks;
using VIMS.API.Controllers;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using Xunit;

namespace VIMS.API.Tests
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthService> _authServiceMock;
        private readonly AuthController _authController;

        public AuthControllerTests()
        {
            _authServiceMock = new Mock<IAuthService>();
            _authController = new AuthController(_authServiceMock.Object);
        }

        [Fact]
        public async Task RegisterCustomer_ShouldReturnOk_WhenSuccessful()
        {
            // Arrange
            var registerDto = new RegisterDTO { Email = "test@example.com", Password = "Password123" };
            _authServiceMock.Setup(service => service.RegisterCustomerAsync(registerDto))
                .ReturnsAsync(new User());

            // Act
            var result = await _authController.RegisterCustomerAsync(registerDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Customer Registered Successfully", okResult.Value);
        }

        [Fact]
        public async Task Login_ShouldReturnOkWithAuthResult_WhenCredentialsAreValid()
        {
            // Arrange
            var loginDto = new LoginDTO { Email = "user@example.com", Password = "Password123" };
            var authResult = new AuthResultDTO { token = "valid-token", name = "Test User" };
            _authServiceMock.Setup(service => service.UserLoginAsync(loginDto))
                .ReturnsAsync(authResult);

            // Act
            var result = await _authController.CustomerLoginAsync(loginDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var resultValue = Assert.IsType<AuthResultDTO>(okResult.Value);
            Assert.Equal("valid-token", resultValue.token);
        }
    }
}
