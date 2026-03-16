using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using VIMS.API.Controllers;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using Xunit;

namespace VIMS.API.Tests
{
    public class AdminControllerTests
    {
        private readonly Mock<IAdminService> _adminServiceMock;
        private readonly Mock<IPolicyPlanService> _planServiceMock;
        private readonly Mock<IClaimsService> _claimsServiceMock;
        private readonly Mock<IAuditService> _auditMock;

        private readonly Mock<IInvoiceService> _invoiceMock;
        private readonly AdminController _adminController;


        public AdminControllerTests()
        {
            _adminServiceMock = new Mock<IAdminService>();
            _planServiceMock = new Mock<IPolicyPlanService>();
            _claimsServiceMock = new Mock<IClaimsService>();
            _auditMock = new Mock<IAuditService>();
            _invoiceMock = new Mock<IInvoiceService>();

            _adminController = new AdminController(
                _adminServiceMock.Object,
                _planServiceMock.Object,
                _claimsServiceMock.Object,
                _auditMock.Object,
                _invoiceMock.Object);
        }


        [Fact]
        public async Task CreateAgent_ShouldReturnOk_WhenSuccessful()
        {
            // Arrange
            var dto = new RegisterDTO { Email = "agent@vims.com" };
            _adminServiceMock.Setup(s => s.CreateAgentAsync(dto))
                .ReturnsAsync(new ProvisioningResultDTO { User = new User { Email = "agent@vims.com" }, WebhookResponse = "Success" });


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
