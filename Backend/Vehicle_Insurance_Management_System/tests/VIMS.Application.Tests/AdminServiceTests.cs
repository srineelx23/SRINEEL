using AutoMapper;
using Moq;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Application.Exceptions;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Application.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using Xunit;

namespace VIMS.Application.Tests
{
    public class AdminServiceTests
    {
        private readonly Mock<IAdminRepository> _adminRepoMock;
        private readonly Mock<IAuthRepository> _authRepoMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IAuditService> _auditServiceMock;
        private readonly AdminService _adminService;

        public AdminServiceTests()
        {
            _adminRepoMock = new Mock<IAdminRepository>();
            _authRepoMock = new Mock<IAuthRepository>();
            _mapperMock = new Mock<IMapper>();
            _auditServiceMock = new Mock<IAuditService>();
            _adminService = new AdminService(_adminRepoMock.Object, _authRepoMock.Object, _mapperMock.Object, _auditServiceMock.Object);
        }

        [Fact]
        public async Task CreateAgentAsync_ShouldThrowException_WhenUserAlreadyExists()
        {
            // Arrange
            var registerDto = new RegisterDTO { Email = "agent@example.com", Password = "Password123" };
            _authRepoMock.Setup(repo => repo.UserExistsAsync(registerDto.Email))
                .ReturnsAsync(new User());

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() => _adminService.CreateAgentAsync(registerDto));
        }

        [Fact]
        public async Task CreateAgentAsync_ShouldSucceed_WhenUserDoesNotExist()
        {
            // Arrange
            var registerDto = new RegisterDTO { Email = "newagent@example.com", Password = "Password123" };
            var agent = new User { Email = "newagent@example.com", Role = UserRole.Agent };

            _authRepoMock.Setup(repo => repo.UserExistsAsync(registerDto.Email))
                .ReturnsAsync((User)null);
            _mapperMock.Setup(m => m.Map<User>(registerDto))
                .Returns(agent);
            _adminRepoMock.Setup(repo => repo.CreateAgentAsync(agent))
                .ReturnsAsync(agent);

            // Act
            var result = await _adminService.CreateAgentAsync(registerDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(UserRole.Agent, result.Role);
            _adminRepoMock.Verify(repo => repo.CreateAgentAsync(It.IsAny<User>()), Times.Once);
        }
    }
}
