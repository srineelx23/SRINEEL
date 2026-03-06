using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IPaymentRepository
    {
        Task<Payment> AddAsync(Payment payment);
        Task<bool> HasUnpaidAsync(int policyId);
        Task<List<Payment>> GetAllAsync();
        Task<List<Payment>> GetByPolicyIdAsync(int policyId);
        Task<Payment?> GetByIdWithDetailsAsync(int paymentId);
    }
}
