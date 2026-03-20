using System;
using System.ComponentModel.DataAnnotations;

namespace VIMS.Domain.Entities
{
    public class Garage
    {
        [Key]
        public int GarageId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string GarageName { get; set; } = string.Empty;
        
        [Required]
        public double Latitude { get; set; }
        
        [Required]
        public double Longitude { get; set; }
        
        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
