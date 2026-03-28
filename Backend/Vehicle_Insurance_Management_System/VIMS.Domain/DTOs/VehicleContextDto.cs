namespace VIMS.Domain.DTOs
{
    public class VehicleContextDto
    {
        public int VehicleId { get; set; }
        public int CustomerId { get; set; }
        public string RegistrationNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public string FuelType { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
    }
}