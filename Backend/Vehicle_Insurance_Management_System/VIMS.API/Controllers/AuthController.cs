using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
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

        //[HttpPost("admin/createAgent")]
    }
}
