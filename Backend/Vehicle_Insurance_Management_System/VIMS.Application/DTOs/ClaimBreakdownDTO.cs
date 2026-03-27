using System.Collections.Generic;

namespace VIMS.Application.DTOs
{
    public class ClaimBreakdownDTO
    {
        public decimal FinalPayout { get; set; }
        public decimal IDV { get; set; }
        public decimal Deductible { get; set; }
        public decimal MaxCoverage { get; set; }
        public bool IsTotalLoss { get; set; }
        public bool IsCapped { get; set; }
        public string? WarningMessage { get; set; }
        public List<BreakdownItemDTO> Items { get; set; } = new List<BreakdownItemDTO>();
    }

    public class BreakdownItemDTO
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string? Status { get; set; } // "success", "error", "warning", null
        public string? Note { get; set; }
    }
}
