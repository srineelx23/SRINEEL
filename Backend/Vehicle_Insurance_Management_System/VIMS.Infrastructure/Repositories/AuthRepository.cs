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
            _context.SaveChanges();
            return customer;
        }
        public async Task<User> RegisterAdminAsync(User admin)
        {
            _context.Users.Add(admin);
            _context.SaveChanges();
            return admin;
        }
    }
}
