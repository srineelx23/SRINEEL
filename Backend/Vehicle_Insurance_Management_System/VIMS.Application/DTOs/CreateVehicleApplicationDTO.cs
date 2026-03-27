using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VIMS.Application.DTOs
{
    public class CreateVehicleApplicationDTO
    {
        [Range(1, int.MaxValue, ErrorMessage = "Plan is required.")]
        public int PlanId { get; set; }

        [Required(ErrorMessage = "Registration number is required.")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Registration number must be between 6 and 20 characters.")]
        public string RegistrationNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vehicle make is required.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "Vehicle make must be between 2 and 60 characters.")]
        public string Make { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vehicle model is required.")]
        [StringLength(80, MinimumLength = 1, ErrorMessage = "Vehicle model must be between 1 and 80 characters.")]
        public string Model { get; set; } = string.Empty;

        [Range(1980, 2100, ErrorMessage = "Manufacture year is out of allowed range.")]
        public int Year { get; set; }

        [Required(ErrorMessage = "Fuel type is required.")]
        public string FuelType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vehicle usage type is required.")]
        public string VehicleType { get; set; } = string.Empty;

        [Range(0, 999999, ErrorMessage = "Kilometers driven must be between 0 and 999999.")]
        public int KilometersDriven { get; set; }

        [Range(1, 5, ErrorMessage = "Policy years must be between 1 and 5.")]
        public int PolicyYears { get; set; }

        [Range(typeof(decimal), "1", "999999999", ErrorMessage = "Invoice amount must be greater than 0.")]
        public decimal InvoiceAmount { get; set; }

        [Required(ErrorMessage = "Invoice document is required.")]
        public IFormFile InvoiceDocument { get; set; }

        [Required(ErrorMessage = "RC document is required.")]
        public IFormFile RcDocument { get; set; }
    }
}
