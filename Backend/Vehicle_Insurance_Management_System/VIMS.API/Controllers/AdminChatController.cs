using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;

namespace VIMS.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/admin")]
    [ApiController]
    public class AdminChatController : ControllerBase
    {
        private readonly IChatOrchestratorService _chatOrchestratorService;

        public AdminChatController(IChatOrchestratorService chatOrchestratorService)
        {
            _chatOrchestratorService = chatOrchestratorService;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(new
                {
                    answer = "Insufficient data to answer.",
                    reasoning = "Question is required.",
                    rulesApplied = Array.Empty<string>(),
                    confidence = "LOW"
                });
            }

            var response = await _chatOrchestratorService.ProcessAdminQueryAsync(request, cancellationToken);

            return Ok(new
            {
                answer = response.Answer,
                reasoning = response.Reasoning,
                rulesApplied = response.RulesApplied,
                confidence = response.Confidence
            });
        }
    }
}
