using System.Text.Json.Serialization;

namespace VIMS.Application.DTOs
{
    public class RoadsideAssistanceDTO
    {
        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("vehicleNumber")]
        public string VehicleNumber { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("garageName")]
        public string GarageName { get; set; } = string.Empty;

        [JsonPropertyName("garagePhone")]
        public string GaragePhone { get; set; } = string.Empty;

        [JsonPropertyName("distance")]
        public double Distance { get; set; }
    }
}
