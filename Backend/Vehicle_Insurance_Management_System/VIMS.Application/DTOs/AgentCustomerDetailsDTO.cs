using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VIMS.Application.DTOs
{
        public class AgentCustomerDetailsDTO
        {
            public int CustomerId { get; set; }
            public string CustomerName { get; set; }
            public string Email { get; set; }

            public List<VehicleDetailsDTO> Vehicles { get; set; } = new();
        }

        public class VehicleDetailsDTO
        {
            public int VehicleId { get; set; }
            public string RegistrationNumber { get; set; }
            public string Make { get; set; }
            public string Model { get; set; }
            public int Year { get; set; }

            public List<string> Documents { get; set; } = new();
            public List<PolicyDetailsDTO> Policies { get; set; } = new();
        }

        public class PolicyDetailsDTO
        {
            public int PolicyId { get; set; }
            public string PolicyNumber { get; set; }
            public string Status { get; set; }
            public decimal? PremiumAmount { get; set; }
        }
}
