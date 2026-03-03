using AutoMapper;
using Moq;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Application.Exceptions;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Application.Services;
using VIMS.Domain.Entities;
using Xunit;

namespace VIMS.Application.Tests
{
    public class AuthServiceTests
    {
        private readonly Mock<IAuthRepository> _authRepoMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IJwtService> _jwtServiceMock;
        private readonly Mock<IAuditService> _auditServiceMock;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _authRepoMock = new Mock<IAuthRepository>();
            _mapperMock = new Mock<IMapper>();
            _jwtServiceMock = new Mock<IJwtService>();
            _auditServiceMock = new Mock<IAuditService>();
            _authService = new AuthService(_authRepoMock.Object, _mapperMock.Object, _jwtServiceMock.Object, _auditServiceMock.Object);
        }

        [Fact]
        public async Task RegisterCustomerAsync_ShouldThrowException_WhenUserAlreadyExists()
        {
            // Arrange
            var registerDto = new RegisterDTO { Email = "existing@example.com", Password = "Password123" };
            _authRepoMock.Setup(repo => repo.UserExistsAsync(registerDto.Email))
                .ReturnsAsync(new User());

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterCustomerAsync(registerDto));
        }

        [Fact]
        public async Task RegisterCustomerAsync_ShouldSucceed_WhenUserDoesNotExist()
        {
            // Arrange
            var registerDto = new RegisterDTO { Email = "new@example.com", Password = "Password123", FullName = "New User" };
            var user = new User { Email = "new@example.com", FullName = "New User" };

            _authRepoMock.Setup(repo => repo.UserExistsAsync(registerDto.Email))
                .ReturnsAsync((User)null);
            _mapperMock.Setup(m => m.Map<User>(registerDto))
                .Returns(user);
            _authRepoMock.Setup(repo => repo.RegisterCustomerAsync(user))
                .ReturnsAsync(user);

            // Act
            var result = await _authService.RegisterCustomerAsync(registerDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("new@example.com", result.Email);
            _authRepoMock.Verify(repo => repo.RegisterCustomerAsync(It.IsAny<User>()), Times.Once);
        }
    }
}
