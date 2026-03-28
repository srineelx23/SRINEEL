using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;

namespace VIMS.API.Controllers
{
    [Authorize(Roles = "Customer")]
    [Route("api/[controller]")]
    [ApiController]
    public class ReferralController : ControllerBase
    {
        private readonly IReferralService _referralService;
        private readonly IWalletService _walletService;

        public ReferralController(IReferralService referralService, IWalletService walletService)
        {
            _referralService = referralService;
            _walletService = walletService;
        }

        [HttpPost("apply")]
        public async Task<IActionResult> ApplyReferralCode([FromBody] ApplyReferralCodeDTO dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _referralService.ApplyReferralCodeAsync(userId, dto.ReferralCode);
            return Ok(new { message = "Referral code applied successfully." });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetReferralHistory()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var history = await _referralService.GetReferralHistoryAsync(userId);
            return Ok(history);
        }

        [HttpGet("wallet/balance")]
        public async Task<IActionResult> GetWalletBalance()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var balance = await _walletService.GetBalanceAsync(userId);
            return Ok(new { balance });
        }
    }
}
