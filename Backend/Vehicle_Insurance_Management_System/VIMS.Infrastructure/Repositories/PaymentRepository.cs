using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Entities;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly VehicleInsuranceContext _context;
        public PaymentRepository(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<Payment> AddAsync(Payment payment)
        {
            await _context.Payments.AddAsync(payment);
            await _context.SaveChangesAsync();
            return payment;
        }

        public async Task<bool> HasUnpaidAsync(int policyId)
        {
            // A simple unpaid check: any payment for this policy with Status != Paid
            return await _context.Payments.AnyAsync(p => p.PolicyId == policyId && p.Status != VIMS.Domain.Enums.PaymentStatus.Paid);
        }

        public async Task<List<Payment>> GetAllAsync()
        {
            return await _context.Payments
                .Include(p => p.Policy)
                .ToListAsync();
        }

        public async Task<List<Payment>> GetByPolicyIdAsync(int policyId)
        {
            return await _context.Payments
                .Where(p => p.PolicyId == policyId)
                .ToListAsync();
        }

        public async Task<Payment?> GetByIdWithDetailsAsync(int paymentId)
        {
            return await _context.Payments
                .Include(p => p.Policy)
                    .ThenInclude(po => po.Customer)
                .Include(p => p.Policy)
                    .ThenInclude(po => po.Vehicle)
                        .ThenInclude(v => v.VehicleApplication)
                .Include(p => p.Policy)
                    .ThenInclude(po => po.Plan)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        }
    }
}

