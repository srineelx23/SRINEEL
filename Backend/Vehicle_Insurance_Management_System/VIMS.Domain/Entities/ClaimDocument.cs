using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VIMS.Domain.Entities
{
    public class ClaimDocument
    {
        [Key]
        public int DocumentId { get; set; }
        public int ClaimId { get; set; }
        public string Document1 { get; set; } = string.Empty;
        public string Document2 { get; set; }
        // Navigation
        public Claims Claim { get; set; } = null!;
    }
}
