using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Services
{
    public interface IAgentService
    {
        public Task ReviewApplicationAsync(int applicationId, ReviewVehicleApplicationDTO dto);
        Task<List<VehicleApplication>> GetMyPendingApplicationsAsync(int agentId);
        public Task<List<AgentCustomerDetailsDTO>> GetMyApprovedCustomersAsync(int agentId);
        public Task<List<VehicleApplication>> GetMyApplicationsAsync(int agentId);
    }
}
