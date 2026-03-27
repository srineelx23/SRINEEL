using System.Collections.Generic;

namespace VIMS.Application.DTOs
{
    public class AgentApplicationValidationResultDTO
    {
        public int RiskScore { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
