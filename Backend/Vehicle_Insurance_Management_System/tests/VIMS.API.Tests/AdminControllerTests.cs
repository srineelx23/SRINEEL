using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using VIMS.API.Controllers;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using Xunit;

namespace VIMS.API.Tests
{
    public class AdminControllerTests
    {
        private readonly Mock<IAdminService> _adminServiceMock;
        private readonly Mock<IPolicyPlanService> _planServiceMock;
        private readonly Mock<IClaimsRepository> _claimsRepoMock;
        private readonly Mock<IPaymentRepository> _payRepoMock;
        private readonly Mock<IPolicyRepository> _polRepoMock;
        private readonly Mock<IAuditService> _auditMock;
        private readonly AdminController _adminController;

        public AdminControllerTests()
        {
            _adminServiceMock = new Mock<IAdminService>();
            _planServiceMock = new Mock<IPolicyPlanService>();
            _claimsRepoMock = new Mock<IClaimsRepository>();
            _payRepoMock = new Mock<IPaymentRepository>();
            _polRepoMock = new Mock<IPolicyRepository>();
            _auditMock = new Mock<IAuditService>();

            _adminController = new AdminController(
                _adminServiceMock.Object, _planServiceMock.Object, _claimsRepoMock.Object,
                _payRepoMock.Object, _polRepoMock.Object, _auditMock.Object);
        }

        [Fact]
        public async Task CreateAgent_ShouldReturnOk_WhenSuccessful()
        {
            // Arrange
            var dto = new RegisterDTO { Email = "agent@vims.com" };
            _adminServiceMock.Setup(s => s.CreateAgentAsync(dto))
                .ReturnsAsync(new User { Email = "agent@vims.com" });

            // Act
            var result = await _adminController.CreateAgentAsync(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetAllUsers_ShouldReturnOk_WithUserList()
        {
            // Arrange
            _adminServiceMock.Setup(s => s.GetAllUsersAsync())
                .ReturnsAsync(new List<User> { new User { FullName = "Admin" } });

            // Act
            var result = await _adminController.GetAllUsers();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var users = Assert.IsType<List<User>>(okResult.Value);
            Assert.Single(users);
        }
    }
}
