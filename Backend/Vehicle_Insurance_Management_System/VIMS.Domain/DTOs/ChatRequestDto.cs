using System.ComponentModel.DataAnnotations;

namespace VIMS.Domain.DTOs
{
    public class ChatRequestDto
    {
        [Required]
        public string Question { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        public List<string> History { get; set; } = new();
    }
}
