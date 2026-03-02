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
    public class AdminService:IAdminService
    {
        private readonly IAdminRepository _adminRepository;
        private readonly IAuthRepository _authRepository;
        private readonly IMapper _mapper;
        private readonly PasswordHasher<User> _passwordHasher;
        public AdminService(IAdminRepository repository,IAuthRepository authRepository,IMapper mapper) {
            _authRepository = authRepository;
            _adminRepository = repository;
            _mapper = mapper;
            _passwordHasher= new PasswordHasher<User>();
        }

        public async Task<User> CreateAgentAsync(RegisterDTO registerDTO)
        {
            var res = await _authRepository.UserExistsAsync(registerDTO.Email);
            if (res != null)
            {
                throw new BadRequestException("Agent already Exists");
            }
            var createdAgent=_mapper.Map<User>(registerDTO);
            createdAgent.Role = UserRole.Agent;
            createdAgent.PasswordHash = _passwordHasher.HashPassword(createdAgent,registerDTO.Password);
            var result=await _adminRepository.CreateAgentAsync(createdAgent);
            return result;
        }
        public async Task<User> CreateClaimsOfficerAsync(RegisterDTO registerDTO)
        {
            var res = await _authRepository.UserExistsAsync(registerDTO.Email);
            if (res != null)
            {
                throw new BadRequestException("Claims Officer Already Exists");
            }
            var createdClaimsOfficer = _mapper.Map<User>(registerDTO);
            createdClaimsOfficer.Role = UserRole.ClaimsOfficer;
            createdClaimsOfficer.PasswordHash=_passwordHasher.HashPassword(createdClaimsOfficer,registerDTO.Password);
            var result=await _adminRepository.CreateClaimsOfficerAsync(createdClaimsOfficer);
            return result;
        }

        public async Task<PolicyPlan> CreatePolicyPlanAsync(PolicyPlan policyPlan)
        {
            var result=await _adminRepository.CreatePolicyPlanAsync(policyPlan);
            return result;
        }
        public async Task<List<PolicyPlan>> GetAllPolicyPlansAsync()
        {
            return await _adminRepository.GetAllPolicyPlansAsync();
        }
        public async Task<PolicyPlan?> GetPolicyPlanByIdAsync(int planId)
        {
            var res= await _adminRepository.GetPolicyPlanByIdAsync(planId);
            if (res == null)
            {
                throw new NotFoundException("Plan does not exist");
            }
            return res;
        }
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _adminRepository.GetAllUsersAsync();
        }
    }
}
