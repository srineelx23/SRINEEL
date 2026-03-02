using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Repositories
{
    public interface IAuthRepository
    {
        public Task<User> UserExistsAsync(string email);
        public Task<User> RegisterCustomerAsync(User customer);
        public Task<User> RegisterAdminAsync(User admin);
        //public Task<User> CustomerLoginAsync(string username, string password);
    }
}
