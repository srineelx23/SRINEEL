using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Enums;

namespace VIMS.Domain.Entities
{
    public class PolicyPlan
    {
        [Key]
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string PolicyType { get; set; } = string.Empty;
        public decimal BasePremium { get; set; }

        public int PolicyDurationMonths { get; set; }
        public decimal DeductibleAmount { get; set; }
        // Coverage
        public bool CoversThirdParty { get; set; }
        public bool CoversOwnDamage { get; set; }
        public bool CoversTheft { get; set; }

        // Add-ons
        public bool ZeroDepreciationAvailable { get; set; }
        public bool EngineProtectionAvailable { get; set; }
        public bool RoadsideAssistanceAvailable { get; set; }
        public string ApplicableVehicleType { get; set; } = string.Empty;
        public PlanStatus Status { get; set; } = PlanStatus.Active;
        // Navigation
        public ICollection<Policy>? Policies { get; set; }
    }
}
