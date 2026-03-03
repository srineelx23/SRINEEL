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
using System.Collections.Generic;

namespace VIMS.Application.Tests
{
    public class ClaimsServiceTests
    {
        private readonly Mock<IClaimsRepository> _claimsRepoMock;
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly Mock<IPolicyRepository> _policyRepoMock;
        private readonly Mock<IPricingService> _pricingServiceMock;
        private readonly Mock<IPaymentRepository> _paymentRepoMock;
        private readonly Mock<IAuditService> _auditServiceMock;
        private readonly Mock<IFileStorageService> _fileStorageMock;
        private readonly ClaimsService _claimsService;

        public ClaimsServiceTests()
        {
            _claimsRepoMock = new Mock<IClaimsRepository>();
            _userRepoMock = new Mock<IUserRepository>();
            _policyRepoMock = new Mock<IPolicyRepository>();
            _pricingServiceMock = new Mock<IPricingService>();
            _paymentRepoMock = new Mock<IPaymentRepository>();
            _auditServiceMock = new Mock<IAuditService>();
            _fileStorageMock = new Mock<IFileStorageService>();

            _claimsService = new ClaimsService(
                _claimsRepoMock.Object, _userRepoMock.Object, _policyRepoMock.Object,
                _pricingServiceMock.Object, _paymentRepoMock.Object, _auditServiceMock.Object, _fileStorageMock.Object);
        }

        [Fact]
        public async Task SubmitClaimAsync_ShouldThrowException_WhenPolicyNotFound()
        {
            // Arrange
            var dto = new SubmitClaimDTO { PolicyId = 1 };
            _policyRepoMock.Setup(repo => repo.GetByIdAsync(dto.PolicyId))
                .ReturnsAsync((Policy)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _claimsService.SubmitClaimAsync(dto, 1));
        }

        [Fact]
        public async Task SubmitClaimAsync_ShouldThrowException_WhenActiveClaimExists()
        {
            // Arrange
            var dto = new SubmitClaimDTO { PolicyId = 1 };
            _policyRepoMock.Setup(repo => repo.GetByIdAsync(dto.PolicyId))
                .ReturnsAsync(new Policy { CustomerId = 1 });
            _claimsRepoMock.Setup(repo => repo.ExistsActiveClaimForPolicyAsync(dto.PolicyId))
                .ReturnsAsync(true);

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() => _claimsService.SubmitClaimAsync(dto, 1));
        }

        [Fact]
        public async Task DecideClaimAsync_ShouldThrowException_WhenClaimNotFound()
        {
            // Arrange
            _claimsRepoMock.Setup(repo => repo.GetByIdAsync(1))
                .ReturnsAsync((Claims)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _claimsService.DecideClaimAsync(1, new ApproveClaimDTO(), 1, true));
        }

        [Fact]
        public async Task DecideClaimAsync_ShouldRejectClaim_WhenApproveIsFalse()
        {
            // Arrange
            var claim = new Claims { ClaimId = 1, ClaimsOfficerId = 10, ClaimNumber = "CLM-123" };
            _claimsRepoMock.Setup(repo => repo.GetByIdAsync(1))
                .ReturnsAsync(claim);

            // Act
            var result = await _claimsService.DecideClaimAsync(1, new ApproveClaimDTO(), 10, false);

            // Assert
            Assert.Equal("Claim rejected", result);
            Assert.Equal(ClaimStatus.Rejected, claim.Status);
            _claimsRepoMock.Verify(repo => repo.UpdateAsync(claim), Times.Once);
        }
    }
}
