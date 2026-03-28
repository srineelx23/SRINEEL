using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IUserRepository
    {
        public Task<User?> GetLeastLoadedAgentAsync();
        public Task<User?> GetLeastLoadedClaimsOfficerAsync();
        public Task<User?> GetByIdAsync(int id);
        public Task<User?> GetByEmailAsync(string email);
        public Task<User?> GetByReferralCodeAsync(string referralCode);
        public Task UpdateAsync(User user);
    }
}
