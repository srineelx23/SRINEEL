using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;

namespace VIMS.API.Controllers
{
    [Authorize(Roles = "Customer,Agent,ClaimsOfficer,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class ChatbotController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;

        public ChatbotController(IChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatbotQueryDTO dto, CancellationToken cancellationToken)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Query))
            {
                return BadRequest(new { message = "Query is required." });
            }

            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var roleValue = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrWhiteSpace(userIdValue) || string.IsNullOrWhiteSpace(roleValue))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            if (!int.TryParse(userIdValue, out var userId))
            {
                return Unauthorized(new { message = "Invalid user id in token." });
            }

            var response = await _chatbotService.AskAsync(userId, roleValue, dto.Query, cancellationToken);
            return Ok(response);
        }
    }
}
