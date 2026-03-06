using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VIMS.Application.DTOs
{
    public class CustomerPolicyDTO
    {
        public int PolicyId { get; set; }
        public string PolicyNumber { get; set; }
        public string PlanName { get; set; }
        public string VehicleRegistrationNumber { get; set; }
        public string VehicleModel { get; set; }
        public decimal PremiumAmount { get; set; }
        public decimal IDV { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; }
        public bool IsRenewed { get; set; }
        public bool IsFeePending { get; set; }
        public string VehicleType { get; set; }
    }
}
