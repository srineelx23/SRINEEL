//using Microsoft.AspNet.Identity;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Application.DTOs;
using VIMS.Application.Exceptions;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepository;
        private readonly IMapper _mapper;
        private readonly PasswordHasher<User> _hasher;
        private readonly IJwtService _jwtService;
        private readonly IAuditService _auditService;
        public AuthService(IAuthRepository authRepository, IMapper mapper, IJwtService service, IAuditService auditService) {
            _authRepository = authRepository;
            _mapper = mapper;
            _hasher = new PasswordHasher<User>();
            _jwtService = service;
            _auditService = auditService;
        }
        public async Task<User> RegisterCustomerAsync(RegisterDTO registerDTO)
        {
            var res = await _authRepository.UserExistsAsync(registerDTO.Email);
            if (res != null)
            {
                throw new BadRequestException("Customer Already Exists");
            }
            var user = _mapper.Map<User>(registerDTO);
            user.PasswordHash = _hasher.HashPassword(user, registerDTO.Password);
            if (!string.IsNullOrEmpty(registerDTO.SecurityAnswer)) {
                user.SecurityAnswerHash = _hasher.HashPassword(user, registerDTO.SecurityAnswer.Trim().ToLower());
            }
            var result = await _authRepository.RegisterCustomerAsync(user);
            await _auditService.LogActionWithUserAsync("CustomerRegister", "Auth", $"Customer {user.FullName} registered.", user.UserId, user.Email, user.Role.ToString(), "User", user.UserId.ToString());
            return result;
        }

        public async Task<User> RegisterAdminAsync(RegisterDTO registerDTO)
        {
            var res=await _authRepository.UserExistsAsync(registerDTO.Email);
            if(res!= null)
            {
                throw new BadRequestException("Admin Already Exists");
            }
            var user= _mapper.Map<User>(registerDTO);
            user.Role = UserRole.Admin;
            user.PasswordHash= _hasher.HashPassword(user,registerDTO.Password);
            if (!string.IsNullOrEmpty(registerDTO.SecurityAnswer)) {
                user.SecurityAnswerHash = _hasher.HashPassword(user, registerDTO.SecurityAnswer.Trim().ToLower());
            }
            var result = await _authRepository.RegisterAdminAsync(user);
            await _auditService.LogActionWithUserAsync("AdminRegister", "Auth", $"Admin {user.FullName} registered.", user.UserId, user.Email, user.Role.ToString(), "User", user.UserId.ToString());
            return result;
        }

        public async Task<AuthResultDTO> UserLoginAsync(LoginDTO dto)
        {
            var customer = await _authRepository.UserExistsAsync(dto.Email);
            if (customer == null)
            {
                throw new NotFoundException("User Does Not Exist");
            }
            var validCredentials = _hasher.VerifyHashedPassword(customer, customer.PasswordHash, dto.Password);
            if (validCredentials == PasswordVerificationResult.Failed)
            {
                throw new BadRequestException("Invalid Credentials");
            }
            var token = _jwtService.GenerateToken(customer);
            var authResult = new AuthResultDTO
            {
                token = token,
                name = customer.FullName,
                Role = customer.Role.ToString(),
                IsSecurityQuestionSet = !string.IsNullOrEmpty(customer.SecurityQuestion)
            };

            await _auditService.LogActionWithUserAsync("Login", "Auth", $"User logged in: {customer.Email}", customer.UserId, customer.Email, customer.Role.ToString());

            return authResult;
        }

        public async Task ChangePasswordAsync(int userId, ChangePasswordDTO dto)
        {
            var user = await _authRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found.");

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword);
            if (result == PasswordVerificationResult.Failed)
                throw new BadRequestException("Current password is incorrect.");

            user.PasswordHash = _hasher.HashPassword(user, dto.NewPassword);
            await _authRepository.UpdateUserAsync(user);
            await _auditService.LogActionAsync("ChangePassword", "Auth", "User changed their password.");
        }
        public async Task<string> GetSecurityQuestionAsync(string email)
        {
            var user = await _authRepository.UserExistsAsync(email);
            if (user == null)
                throw new NotFoundException("Email not found");
            
            if (string.IsNullOrEmpty(user.SecurityQuestion))
                throw new BadRequestException("No security question set for this account.");

            return user.SecurityQuestion;
        }

        public async Task ResetPasswordAsync(ForgotPasswordDTO dto)
        {
            var user = await _authRepository.UserExistsAsync(dto.Email);
            if (user == null)
                throw new NotFoundException("Email not found");

            if (string.IsNullOrEmpty(user.SecurityAnswerHash))
                throw new BadRequestException("No security answer set for this account.");

            var isAnswerValid = _hasher.VerifyHashedPassword(user, user.SecurityAnswerHash, dto.SecurityAnswer.Trim().ToLower());
            if (isAnswerValid == PasswordVerificationResult.Failed)
                throw new BadRequestException("Incorrect security answer.");

            user.PasswordHash = _hasher.HashPassword(user, dto.NewPassword);
            await _authRepository.UpdateUserAsync(user);
            await _auditService.LogActionWithUserAsync("ResetPassword", "Auth", $"User reset password for: {user.Email}", user.UserId, user.Email, user.Role.ToString());
        }

        public async Task SetSecurityQuestionAsync(SetSecurityQuestionDTO dto)
        {
            var user = await _authRepository.UserExistsAsync(dto.Email);
            if (user == null)
                throw new NotFoundException("User not found");

            user.SecurityQuestion = dto.SecurityQuestion;
            user.SecurityAnswerHash = _hasher.HashPassword(user, dto.SecurityAnswer.Trim().ToLower());

            await _authRepository.UpdateUserAsync(user);
        }
    }
}
