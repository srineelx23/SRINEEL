using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VIMS.Application.DTOs
{
    public class CalculateQuoteDTO
    {
            public decimal InvoiceAmount { get; set; }
            public int ManufactureYear { get; set; }
            public string FuelType { get; set; }
            public string VehicleType { get; set; }
            public int KilometersDriven { get; set; }
            public int PolicyYears { get; set; }
            public int PlanId { get; set; }
    }
}
