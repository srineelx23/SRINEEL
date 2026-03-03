using Microsoft.AspNetCore.Http;

namespace VIMS.Application.DTOs
{
    public class AcceptTransferDTO
    {
        public IFormFile? RcDocument { get; set; }
    }
}
