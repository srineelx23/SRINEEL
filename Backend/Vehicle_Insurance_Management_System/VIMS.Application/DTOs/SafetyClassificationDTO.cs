using System.Text.Json.Serialization;

namespace VIMS.Application.DTOs
{
    public class SafetyClassificationDTO
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("intent")]
        public string Intent { get; set; } = string.Empty;
    }
}
