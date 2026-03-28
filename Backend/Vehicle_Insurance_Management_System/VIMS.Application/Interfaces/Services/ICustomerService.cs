using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VIMS.Application.DTOs;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.Application.Interfaces.Services
{
    public interface ICustomerService
    {
        Task CreateApplicationAsync(CreateVehicleApplicationDTO dto,int userId);
        public Task<IEnumerable<object>> ViewAllPoliciesAsync();
        public Task<List<CustomerApplicationDTO>> GetMyApplicationsAsync(int customerId);
        Task<List<CustomerPolicyDTO>> GetMyPoliciesAsync(int customerId);
        public Task<string> RenewPolicyAsync(int policyId,RenewPolicyDTO dto,int customerId);
        public Task<string> PayAnnualPremiumAsync(int policyId, int customerId, decimal? amountOverride = null, PaymentMethod paymentMethod = PaymentMethod.NetBanking, string? transactionReference = null);

        // Policy Transfer
        Task<string> InitiateTransferAsync(InitiateTransferDTO dto, int senderCustomerId);
        Task<List<object>> GetMyIncomingTransfersAsync(int recipientCustomerId);
        Task<List<object>> GetMyOutgoingTransfersAsync(int senderCustomerId);
        Task<string> AcceptTransferAsync(int transferId, IFormFile rcDocument, int recipientCustomerId);
        Task<string> RejectTransferAsync(int transferId, int recipientCustomerId);
    }
}
