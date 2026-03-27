using Moq;
using System.Security.Claims;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Application.Exceptions;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Application.Services;
using VIMS.Domain.Entities;
using Xunit;
using System.Collections.Generic;

namespace VIMS.Application.Tests
{
    public class CustomerServiceTests
    {
        private readonly Mock<ICustomerRepository> _custRepoMock;
        private readonly Mock<IVehicleApplicationRepository> _appRepoMock;
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly Mock<IVehicleRepository> _vhRepoMock;
        private readonly Mock<IPolicyRepository> _polRepoMock;
        private readonly Mock<IPaymentRepository> _payRepoMock;
        private readonly Mock<IPricingService> _priceMock;
        private readonly Mock<IPolicyPlanService> _planMock;
        private readonly Mock<IPolicyTransferRepository> _transMock;
        private readonly Mock<IAuditService> _auditMock;
        private readonly Mock<IFileStorageService> _fileStorageMock;
        private readonly Mock<IClaimsRepository> _claimsRepoMock;
        private readonly Mock<INotificationService> _notifServiceMock;
        private readonly CustomerService _customerService;

        public CustomerServiceTests()
        {
            _custRepoMock = new Mock<ICustomerRepository>();
            _appRepoMock = new Mock<IVehicleApplicationRepository>();
            _userRepoMock = new Mock<IUserRepository>();
            _vhRepoMock = new Mock<IVehicleRepository>();
            _polRepoMock = new Mock<IPolicyRepository>();
            _payRepoMock = new Mock<IPaymentRepository>();
            _priceMock = new Mock<IPricingService>();
            _planMock = new Mock<IPolicyPlanService>();
            _transMock = new Mock<IPolicyTransferRepository>();
            _auditMock = new Mock<IAuditService>();
            _fileStorageMock = new Mock<IFileStorageService>();
            _claimsRepoMock = new Mock<IClaimsRepository>();
            _notifServiceMock = new Mock<INotificationService>();

            _customerService = new CustomerService(
                _custRepoMock.Object, _appRepoMock.Object, _userRepoMock.Object,
                _vhRepoMock.Object, _polRepoMock.Object, _payRepoMock.Object,
                _priceMock.Object, _planMock.Object, _transMock.Object, 
                _auditMock.Object, _fileStorageMock.Object, _claimsRepoMock.Object, _notifServiceMock.Object);
        }

        [Fact]
        public async Task CreateApplicationAsync_ShouldThrowException_WhenRegNumberInvalid()
        {
            // Arrange
            var dto = new CreateVehicleApplicationDTO { RegistrationNumber = "INVALID" };

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() => _customerService.CreateApplicationAsync(dto, 1));
        }

        [Fact]
        public async Task CreateApplicationAsync_ShouldThrowException_WhenVehicleAlreadyInsured()
        {
            // Arrange
            var dto = new CreateVehicleApplicationDTO { RegistrationNumber = "KA01AB1234" };
            _vhRepoMock.Setup(r => r.GetByRegistrationNumberAsync("KA01AB1234"))
                .ReturnsAsync(new Vehicle());

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() => _customerService.CreateApplicationAsync(dto, 1));
        }

        [Fact]
        public async Task GetMyPoliciesAsync_ShouldReturnEmpty_WhenNoPoliciesFound()
        {
            // Arrange
            _polRepoMock.Setup(r => r.GetPoliciesByCustomerIdAsync(1))
                .ReturnsAsync(new List<Policy>());

            // Act
            var result = await _customerService.GetMyPoliciesAsync(1);

            // Assert
            Assert.Empty(result);
        }
    }
}
