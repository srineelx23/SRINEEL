namespace VIMS.Application.DTOs
{
    public class ChatbotQueryDTO
    {
        public string Query { get; set; } = string.Empty;
        public List<ChatHistoryItemDTO> History { get; set; } = new();
    }

    public class ChatHistoryItemDTO
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
