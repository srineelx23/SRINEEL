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
        public int PlanId { get; set; }
        public string RegistrationNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public string FuelType { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public int KilometersDriven { get; set; }
        public int PolicyYears { get; set; }
        public decimal InvoiceAmount { get; set; }
        public IFormFile InvoiceDocument { get; set; }
        public IFormFile RcDocument { get; set; }
    }
}
