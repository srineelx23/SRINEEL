using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;
using VIMS.Infrastructure.Persistence;

namespace VIMS.Infrastructure.Repositories
{
    public class AuthRepository : IAuthRepository
    {
        private readonly VehicleInsuranceContext _context;
        public AuthRepository(VehicleInsuranceContext context) {
            _context = context;
        }
        public async Task<User> UserExistsAsync(string email)
        {
            var res = await _context.Users.FirstOrDefaultAsync(u=> u.Email == email);
            return res;
        }
        public async Task<User> RegisterCustomerAsync(User customer)
        {
            _context.Users.Add(customer);
            await _context.SaveChangesAsync();

            if (customer.ReferredByUserId.HasValue)
            {
                var existingReferral = await _context.Referrals.FirstOrDefaultAsync(r => r.RefereeUserId == customer.UserId);
                if (existingReferral == null)
                {
                    await _context.Referrals.AddAsync(new Referral
                    {
                        ReferrerUserId = customer.ReferredByUserId.Value,
                        RefereeUserId = customer.UserId,
                        Status = ReferralStatus.Pending,
                        DiscountAmount = 0,
                        RewardAmount = 0,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
            }

            return customer;
        }
        public async Task<User> RegisterAdminAsync(User admin)
        {
            _context.Users.Add(admin);
            await _context.SaveChangesAsync();
            return admin;
        }

        public async Task<User> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        public async Task UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task<User?> GetUserByReferralCodeAsync(string referralCode)
        {
            var normalizedCode = (referralCode ?? string.Empty).Trim().ToUpper();
            return await _context.Users.FirstOrDefaultAsync(u => u.ReferralCode != null && u.ReferralCode.ToUpper() == normalizedCode);
        }
    }
}
