using System;

namespace VIMS.Application.DTOs
{
    public class OcrExtractionResultDTO
    {
        public string RegistrationNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public string FuelType { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        /// <summary>
        /// Normalized vehicle category: Car, TwoWheeler, ThreeWheeler, HeavyVehicle
        /// Used to validate against the plan's ApplicableVehicleType.
        /// </summary>
        public string VehicleClass { get; set; } = string.Empty;
        public decimal InvoiceAmount { get; set; }
    }
}
