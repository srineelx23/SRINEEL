using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.Application.Services
{
    public class ChatbotService : IChatbotService
    {
        private const string FallbackAnswer = "I don't have that information";

        private readonly IPolicyRepository _policyRepository;
        private readonly IPolicyPlanRepository _policyPlanRepository;
        private readonly IClaimsRepository _claimsRepository;
        private readonly IVehicleApplicationRepository _applicationRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly VertexAgentService _vertexAgentService;
        private readonly QueryExecutionService _queryExecutionService;
        private readonly IHybridRuleEngineService _hybridRuleEngineService;

        public ChatbotService(
            IPolicyRepository policyRepository,
            IPolicyPlanRepository policyPlanRepository,
            IClaimsRepository claimsRepository,
            IVehicleApplicationRepository applicationRepository,
            IPaymentRepository paymentRepository,
            VertexAgentService vertexAgentService,
            QueryExecutionService queryExecutionService,
            IHybridRuleEngineService hybridRuleEngineService)
        {
            _policyRepository = policyRepository;
            _policyPlanRepository = policyPlanRepository;
            _claimsRepository = claimsRepository;
            _applicationRepository = applicationRepository;
            _paymentRepository = paymentRepository;
            _vertexAgentService = vertexAgentService;
            _queryExecutionService = queryExecutionService;
            _hybridRuleEngineService = hybridRuleEngineService;
        }

        public async Task<ChatbotResponseDTO> AskAsync(int userId, string role, string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new ChatbotResponseDTO { Answer = SanitizeForUi(FallbackAnswer), Role = role };
            }

            if (!Enum.TryParse<UserRole>(role, true, out var parsedRole))
            {
                return new ChatbotResponseDTO { Answer = SanitizeForUi(FallbackAnswer), Role = role };
            }

            var authorizedContext = await GetAuthorizedContextAsync(userId, parsedRole);

            var decision = await _vertexAgentService.GetDecisionAsync(query, cancellationToken);
            
            if (decision == null || string.IsNullOrWhiteSpace(decision.Entity))
            {
                return new ChatbotResponseDTO { Answer = SanitizeForUi(FallbackAnswer), RetrievedChunks = 0, Role = role };
            }

            if (string.Equals(decision.Intent, "RULE_QUERY", StringComparison.OrdinalIgnoreCase))
            {
                var ruleResult = await _hybridRuleEngineService.ExecuteAsync(query, cancellationToken);
                return new ChatbotResponseDTO
                {
                    Answer = SanitizeForUi(ruleResult),
                    RetrievedChunks = 0,
                    Role = role
                };
            }

            var result = await _queryExecutionService.ExecuteAsync(
                query,
                decision,
                userId,
                parsedRole.ToString(),
                authorizedContext.Policies,
                authorizedContext.Claims,
                authorizedContext.Applications,
                authorizedContext.Payments,
                authorizedContext.Plans);

            return new ChatbotResponseDTO
            {
                Answer = SanitizeForUi(string.IsNullOrWhiteSpace(result) ? FallbackAnswer : result),
                RetrievedChunks = 0,
                Role = role
            };
        }

        private static string SanitizeForUi(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            return text.Replace("*", string.Empty);
        }

        private async Task<(List<Policy> Policies, List<Claims> Claims, List<VehicleApplication> Applications, List<Payment> Payments, List<PolicyPlan> Plans)> GetAuthorizedContextAsync(int userId, UserRole role)
        {
            var policies = new List<Policy>();
            var claims = new List<Claims>();
            var applications = new List<VehicleApplication>();
            var payments = new List<Payment>();
            var plans = new List<PolicyPlan>();

            switch (role)
            {
                case UserRole.Customer:
                    policies = await _policyRepository.GetPoliciesByCustomerIdAsync(userId);
                    claims = await _claimsRepository.GetByCustomerIdAsync(userId);
                    applications = await _applicationRepository.GetByCustomerIdAsync(userId);
                    break;

                case UserRole.Agent:
                    policies = await _policyRepository.GetPoliciesByAgentIdAsync(userId);
                    applications = await _applicationRepository.GetAllByAgentIdAsync(userId);

                    var assignedCustomerIds = policies.Select(p => p.CustomerId)
                        .Concat(applications.Select(a => a.CustomerId))
                        .Distinct()
                        .ToList();

                    claims = await _claimsRepository.GetByCustomerIdsAsync(assignedCustomerIds);
                    break;

                case UserRole.ClaimsOfficer:
                    claims = await _claimsRepository.GetByOfficerIdAsync(userId);
                    policies = claims
                        .Where(c => c.Policy != null)
                        .Select(c => c.Policy)
                        .DistinctBy(p => p.PolicyId)
                        .ToList();
                    break;

                case UserRole.Admin:
                    policies = await _policyRepository.GetAllAsync();
                    claims = await _claimsRepository.GetAllAsync();
                    applications = await _applicationRepository.GetAllAsync();
                    break;
            }

            var policyIds = policies.Select(p => p.PolicyId).Distinct().ToList();
            if (policyIds.Count > 0)
            {
                foreach (var policyId in policyIds)
                {
                    var policyPayments = await _paymentRepository.GetByPolicyIdAsync(policyId);
                    if (policyPayments.Count > 0)
                    {
                        payments.AddRange(policyPayments);
                    }
                }
            }

            plans = await _policyPlanRepository.GetAllAsync();
            if (role != UserRole.Admin)
            {
                plans = plans.Where(p => p.Status == PlanStatus.Active).ToList();
            }

            return (policies, claims, applications, payments, plans);
        }
    }
}
