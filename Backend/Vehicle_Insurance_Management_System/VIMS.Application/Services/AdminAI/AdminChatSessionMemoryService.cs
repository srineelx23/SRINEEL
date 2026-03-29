using System.Collections.Concurrent;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;

namespace VIMS.Application.Services.AdminAI
{
    public class AdminChatSessionMemoryService : IAdminChatSessionMemoryService
    {
        private static readonly ContextMemoryDto EmptyMemory = new();
        private readonly ConcurrentDictionary<string, ContextMemoryDto> _memoryBySession = new(StringComparer.OrdinalIgnoreCase);

        public ContextMemoryDto Get(string sessionId)
        {
            var key = NormalizeSessionId(sessionId);
            if (_memoryBySession.TryGetValue(key, out var memory))
            {
                return Clone(memory);
            }

            return Clone(EmptyMemory);
        }

        public void Upsert(string sessionId, ContextMemoryDto memory)
        {
            var key = NormalizeSessionId(sessionId);
            var snapshot = Clone(memory);
            snapshot.LastUpdatedUtc = DateTime.UtcNow;
            _memoryBySession[key] = snapshot;
        }

        private static string NormalizeSessionId(string? sessionId)
        {
            return string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId.Trim();
        }

        private static ContextMemoryDto Clone(ContextMemoryDto source)
        {
            return new ContextMemoryDto
            {
                LastIntent = source.LastIntent,
                LastEntityId = source.LastEntityId,
                EntityType = source.EntityType,
                AnchorPolicyId = source.AnchorPolicyId,
                AnchorVehicleId = source.AnchorVehicleId,
                LastPlanType = source.LastPlanType,
                LastRangeDays = source.LastRangeDays,
                LastUpdatedUtc = source.LastUpdatedUtc
            };
        }
    }
}
