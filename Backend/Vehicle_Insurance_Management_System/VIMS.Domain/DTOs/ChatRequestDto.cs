using System.ComponentModel.DataAnnotations;

namespace VIMS.Domain.DTOs
{
    public class ChatRequestDto
    {
        [Required]
        public string Question { get; set; } = string.Empty;
    }
}
