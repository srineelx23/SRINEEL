using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VIMS.Domain.Entities
{
    public class VehicleDocument
    {
        [Key]
       public int DocumentId { get; set; }

        public int VehicleApplicationId { get; set; }
        [JsonIgnore]
        public VehicleApplication VehicleApplication { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
