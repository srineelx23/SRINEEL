using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<User> RegisterCustomerAsync(RegisterDTO registerDTO);
        Task<AuthResultDTO> UserLoginAsync(LoginDTO dto);
        Task<User> RegisterAdminAsync(RegisterDTO registerDTO);
        Task ChangePasswordAsync(int userId, ChangePasswordDTO dto);
        Task<string> GetSecurityQuestionAsync(string email);
        Task ResetPasswordAsync(ForgotPasswordDTO dto);
        Task SetSecurityQuestionAsync(SetSecurityQuestionDTO dto);
    }
}
