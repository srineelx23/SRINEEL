namespace VIMS.Application.DTOs
{
    public class ChatbotResponseDTO
    {
        public string Answer { get; set; } = "I don't have that information";
        public int RetrievedChunks { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
