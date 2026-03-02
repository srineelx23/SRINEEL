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
        public AuthService(IAuthRepository authRepository,IMapper mapper,IJwtService service) {
            _authRepository = authRepository;
            _mapper = mapper;
            _hasher = new PasswordHasher<User>();
            _jwtService = service;
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
            return await _authRepository.RegisterCustomerAsync(user);
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
            return await _authRepository.RegisterAdminAsync(user);
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
                Role = customer.Role.ToString()
            };
            return authResult;
        }
    }
}
