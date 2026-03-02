using Microsoft.AspNetCore.Http;
using VIMS.Domain.Enums;
namespace VIMS.Application.DTOs
{
    public class SubmitClaimDTO
    {
        public int PolicyId { get; set; }
        // Accept claim type as string from the client (e.g. "Theft", "Damage", "ThirdParty")
        public VIMS.Domain.Enums.ClaimType ClaimType { get; set; }
        public IFormFile? Document1 { get; set; }
        public IFormFile? Document2 { get; set; }
    }
}
