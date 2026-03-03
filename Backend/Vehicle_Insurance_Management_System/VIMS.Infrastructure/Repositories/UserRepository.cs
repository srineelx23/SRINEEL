using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Repositories
{
    public class UserRepository:IUserRepository
    {
        private readonly VehicleInsuranceContext _context;

        public UserRepository(VehicleInsuranceContext context)
        {
            _context = context;
        }

        public async Task<User?> GetLeastLoadedAgentAsync()
        {
            return await _context.Users
                .Where(u => u.Role == UserRole.Agent && u.IsActive)
                .Select(a => new
                {
                    Agent = a,
                    Count = _context.VehicleApplications.Count(v => v.AssignedAgentId == a.UserId)
                })
                .OrderBy(x => x.Count)
                .ThenBy(x=>x.Agent.UserId)
                .Select(x => x.Agent)
                .FirstOrDefaultAsync();
        }
        public async Task<User?> GetLeastLoadedClaimsOfficerAsync()
        {
            return await _context.Users
                .Where(u => u.Role == UserRole.ClaimsOfficer && u.IsActive)
                .Select(a => new
                {
                    Officer = a,
                    Count = _context.Claims.Count(c => c.ClaimsOfficerId == a.UserId)
                })
                .OrderBy(x => x.Count)
                .ThenBy(x => x.Officer.UserId)
                .Select(x => x.Officer)
                .FirstOrDefaultAsync();
        }
        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }
    }
}
