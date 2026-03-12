using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Entities;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Repositories
{
    public class PolicyTransferRepository : IPolicyTransferRepository
    {
        private readonly VehicleInsuranceContext _context;

        public PolicyTransferRepository(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task AddAsync(PolicyTransfer transfer)
        {
            await _context.PolicyTransfers.AddAsync(transfer);
        }

        public async Task<PolicyTransfer?> GetByIdAsync(int id)
        {
            return await _context.PolicyTransfers
                .Include(t => t.Policy)
                    .ThenInclude(p => p.Vehicle)
                        .ThenInclude(v => v.VehicleApplication)
                            .ThenInclude(va => va.Documents)
                .Include(t => t.Policy)
                    .ThenInclude(p => p.Plan)
                .Include(t => t.SenderCustomer)
                .Include(t => t.RecipientCustomer)
                .Include(t => t.NewVehicleApplication)
                .FirstOrDefaultAsync(t => t.PolicyTransferId == id);
        }

        public async Task<List<PolicyTransfer>> GetBySenderIdAsync(int senderId)
        {
            return await _context.PolicyTransfers
                .Where(t => t.SenderCustomerId == senderId)
                .Include(t => t.Policy)
                    .ThenInclude(p => p.Vehicle)
                .Include(t => t.Policy)
                    .ThenInclude(p => p.Plan)
                .Include(t => t.RecipientCustomer)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<PolicyTransfer>> GetByRecipientIdAsync(int recipientId)
        {
            return await _context.PolicyTransfers
                .Where(t => t.RecipientCustomerId == recipientId
                         && t.Status == VIMS.Domain.Enums.PolicyTransferStatus.PendingRecipientAcceptance)
                .Include(t => t.Policy)
                    .ThenInclude(p => p.Vehicle)
                .Include(t => t.Policy)
                    .ThenInclude(p => p.Plan)
                .Include(t => t.SenderCustomer)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<PolicyTransfer>> GetByNewVehicleApplicationIdAsync(int vehicleApplicationId)
        {
            return await _context.PolicyTransfers
                .Where(t => t.NewVehicleApplicationId == vehicleApplicationId)
                .Include(t => t.Policy)
                    .ThenInclude(p => p.Vehicle)
                .Include(t => t.SenderCustomer)
                .Include(t => t.RecipientCustomer)
                .ToListAsync();
        }

        public async Task<List<PolicyTransfer>> GetTransfersByPolicyIdAsync(int policyId)
        {
            return await _context.PolicyTransfers
                .Where(t => t.PolicyId == policyId)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
