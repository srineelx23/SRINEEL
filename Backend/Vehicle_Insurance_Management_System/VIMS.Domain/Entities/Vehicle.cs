using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VIMS.Domain.Entities
{
    public class Vehicle
    {
        [Key]
        public int VehicleId { get; set; }
        public int CustomerId { get; set; }
        public int VehicleApplicationId { get; set; }
        public VehicleApplication VehicleApplication { get; set; }
        public string RegistrationNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public string FuelType { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        // Navigation
        public User Customer { get; set; } = null!;
        public ICollection<Policy>? Policies { get; set; }
    }
}
