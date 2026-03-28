namespace VIMS.Domain.DTOs
{
    public class GarageContextDto
    {
        public int GarageId { get; set; }
        public string GarageName { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}