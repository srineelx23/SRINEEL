using System.Collections.Generic;

namespace VIMS.Application.DTOs
{
    public class ClaimsAnalysisResultDTO
    {
        public int FraudRiskScore { get; set; }
        public List<string> RiskReasons { get; set; } = new List<string>();
        public string Summary { get; set; } = string.Empty;
        public bool VehicleMismatchDetected { get; set; }
        public string ExtractedVehicleNumber { get; set; } = string.Empty;
        public decimal? SuggestedRepairCost { get; set; }
        public int? SuggestedManufactureYear { get; set; }
    }
}
