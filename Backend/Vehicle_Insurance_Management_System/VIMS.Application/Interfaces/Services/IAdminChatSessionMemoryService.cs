using VIMS.Domain.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IAdminChatSessionMemoryService
    {
        ContextMemoryDto Get(string sessionId);
        void Upsert(string sessionId, ContextMemoryDto memory);
    }
}
