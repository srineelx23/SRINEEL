using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;

namespace VIMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        }

        [HttpPost]
        public async Task<IActionResult> AskQuestion([FromBody] ChatbotQueryDTO request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { Message = "Query is required." });
            }

            try
            {
                var response = await _chatService.AnswerQueryAsync(request.Query, cancellationToken);
                return Ok(new { Response = response });
            }
            catch (Exception ex)
            {
                // In a production app, the innermost exception should be reliably logged here implicitly or via ILogger
                return StatusCode(500, new { Message = "An internal error occurred while processing your query.", Details = ex.Message });
            }
        }
    }
}
