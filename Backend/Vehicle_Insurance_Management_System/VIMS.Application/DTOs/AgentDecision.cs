using System.Collections.Generic;

namespace VIMS.Application.DTOs
{
    public class AgentDecision
    {
        public string Intent { get; set; }
        public string Entity { get; set; }
        public Dictionary<string, string> Filters { get; set; } = new Dictionary<string, string>();
        public string Aggregation { get; set; }
        public string GroupBy { get; set; }
        public string SortBy { get; set; }
        public int Limit { get; set; }
    }
}
