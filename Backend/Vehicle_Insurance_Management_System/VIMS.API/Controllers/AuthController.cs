using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Security.Claims;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;

namespace VIMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService) { 
            _authService = authService;
        }
        [HttpPost]
        [Route("customer/register")]
        public async Task<IActionResult> RegisterCustomerAsync(RegisterDTO registerDTO)
        {
                await _authService.RegisterCustomerAsync(registerDTO);
                return Ok("Customer Registered Successfully");
        }
        [HttpPost("login")]
        public async Task<IActionResult> CustomerLoginAsync(LoginDTO dto)
        {
                var res = await _authService.UserLoginAsync(dto);
                return Ok(res);
        }
        [HttpPost("admin/register")]
        public async Task<IActionResult> RegisterAdminAsync(RegisterDTO registerDTO)
        {
                var res = await _authService.RegisterAdminAsync(registerDTO);
                return Ok(res);
        }

        [Authorize]
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePasswordAsync(ChangePasswordDTO dto)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized("Invalid token.");

            await _authService.ChangePasswordAsync(userId, dto);
            return Ok("Password changed successfully.");
        }

        [HttpGet("forgot-password/security-question/{email}")]
        public async Task<IActionResult> GetSecurityQuestion(string email)
        {
            var question = await _authService.GetSecurityQuestionAsync(email);
            return Ok(new { question });
        }

        [HttpPost("forgot-password/reset")]
        public async Task<IActionResult> ResetPassword([FromBody] ForgotPasswordDTO dto)
        {
            await _authService.ResetPasswordAsync(dto);
            return Ok("Password reset successfully.");
        }

        [Authorize]
        [HttpPost("set-security-question")]
        public async Task<IActionResult> SetSecurityQuestion([FromBody] SetSecurityQuestionDTO dto)
        {
            await _authService.SetSecurityQuestionAsync(dto);
            return Ok("Security question set successfully.");
        }
        [HttpPost("complete-first-login")]
        public async Task<IActionResult> CompleteFirstLogin([FromBody] CompleteFirstLoginDTO dto)
        {
            await _authService.CompleteFirstLoginAsync(dto);
            return Ok("Account setup completed successfully.");
        }
    }
}

