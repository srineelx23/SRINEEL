using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IPolicyTransferRepository
    {
        Task AddAsync(PolicyTransfer transfer);
        Task<PolicyTransfer?> GetByIdAsync(int id);
        Task<List<PolicyTransfer>> GetBySenderIdAsync(int senderId);
        Task<List<PolicyTransfer>> GetByRecipientIdAsync(int recipientId);
        Task<List<PolicyTransfer>> GetByNewVehicleApplicationIdAsync(int vehicleApplicationId);
        Task SaveChangesAsync();
    }
}
