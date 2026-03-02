using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;
using VIMS.Application.Services;

namespace VIMS.API.Controllers
{
    [Authorize(Roles ="Agent")]
    [Route("api/[controller]")]
    [ApiController]
    public class AgentController : ControllerBase
    {
        private readonly IAgentService _service;

        public AgentController(IAgentService service)
        {
            _service = service;
        }

        [HttpPut("vehicle-application/{id}/review")]
        public async Task<IActionResult> Review(int id, ReviewVehicleApplicationDTO dto)
        {
            await _service.ReviewApplicationAsync(id, dto);
            return Ok("Reviewed.");
        }
        [HttpGet("pending-applications")]
        public async Task<IActionResult> GetPendingApplications()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _service.GetMyPendingApplicationsAsync(userId);
            return Ok(result);
        }
        [HttpGet("customers")]
        public async Task<IActionResult> GetMyCustomers()
        {
          
                var agentIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (agentIdClaim == null)
                    return Unauthorized("Invalid token.");

                int agentId = int.Parse(agentIdClaim.Value);

                var result = await _service
                    .GetMyApprovedCustomersAsync(agentId);

                return Ok(result);
         
         
        }
        [HttpGet("applications")]
        public async Task<IActionResult> GetMyApplications()
        {
          
                var agentId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                var applications = await _service
                    .GetMyApplicationsAsync(agentId);

                return Ok(applications);
          
        }
    }
}
