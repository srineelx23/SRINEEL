using Microsoft.Extensions.Logging;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;
using VIMS.Domain.Enums;

namespace VIMS.Application.Services.AdminAI
{
    public class ChatOrchestratorService : IChatOrchestratorService
    {
        private readonly IIntentParser _intentParser;
        private readonly IContextBuilder _contextBuilder;
        private readonly IPromptBuilder _promptBuilder;
        private readonly ILlmService _llmService;
        private readonly IRAGService _ragService;
        private readonly IClaimService _claimService;
        private readonly IUserService _userService;
        private readonly IPolicyService _policyService;
        private readonly IAdminVehicleService _adminVehicleService;
        private readonly IAdminPaymentService _adminPaymentService;
        private readonly IAdminVehicleApplicationService _adminVehicleApplicationService;
        private readonly IAdminGarageService _adminGarageService;
        private readonly IAdminNotificationService _adminNotificationService;
        private readonly IReferralService _referralService;
        private readonly ILogger<ChatOrchestratorService> _logger;

        public ChatOrchestratorService(
            IIntentParser intentParser,
            IContextBuilder contextBuilder,
            IPromptBuilder promptBuilder,
            ILlmService llmService,
            IRAGService ragService,
            IClaimService claimService,
            IUserService userService,
            IPolicyService policyService,
            IAdminVehicleService adminVehicleService,
            IAdminPaymentService adminPaymentService,
            IAdminVehicleApplicationService adminVehicleApplicationService,
            IAdminGarageService adminGarageService,
            IAdminNotificationService adminNotificationService,
            IReferralService referralService,
            ILogger<ChatOrchestratorService> logger)
        {
            _intentParser = intentParser;
            _contextBuilder = contextBuilder;
            _promptBuilder = promptBuilder;
            _llmService = llmService;
            _ragService = ragService;
            _claimService = claimService;
            _userService = userService;
            _policyService = policyService;
            _adminVehicleService = adminVehicleService;
            _adminPaymentService = adminPaymentService;
            _adminVehicleApplicationService = adminVehicleApplicationService;
            _adminGarageService = adminGarageService;
            _adminNotificationService = adminNotificationService;
            _referralService = referralService;
            _logger = logger;
        }

        public async Task<ChatResponseDto> ProcessAdminQueryAsync(ChatRequestDto request, CancellationToken cancellationToken = default)
        {
            var question = request?.Question?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(question))
            {
                return new ChatResponseDto
                {
                    Answer = "Insufficient data to answer.",
                    Reasoning = "Admin question is empty.",
                    Confidence = "LOW"
                };
            }

            _logger.LogInformation("Admin chat question received: {Question}", question);

            try
            {
                var intent = _intentParser.Parse(question);
                var lowerQuestion = question.ToLowerInvariant();

                var claims = new List<ClaimContextDto>();
                var users = new List<UserContextDto>();
                var policies = new List<PolicyContextDto>();
                var vehicles = new List<VehicleContextDto>();
                var payments = new List<PaymentContextDto>();
                PaymentAggregateContextDto? paymentAggregates = null;
                var applications = new List<VehicleApplicationContextDto>();
                var garages = new List<GarageContextDto>();
                var notifications = new List<NotificationContextDto>();
                var referrals = new List<ReferralContextDto>();
                var referralAbuseSignals = new List<ReferralAbuseSignalDto>();

                if (intent.IncludeClaims)
                {
                    if (intent.ClaimId.HasValue)
                    {
                        var claim = await _claimService.GetClaimByIdAsync(intent.ClaimId.Value, cancellationToken);
                        if (claim != null)
                        {
                            claims.Add(claim);
                        }
                    }
                    else if (intent.UserId.HasValue)
                    {
                        var userClaims = await _claimService.GetClaimsByUserIdAsync(intent.UserId.Value, cancellationToken);
                        claims.AddRange(userClaims);
                    }
                    else
                    {
                        if (ContainsAny(lowerQuestion, "rejected", "rejection", "fraud"))
                        {
                            var rejectedClaims = await _claimService.GetRecentRejectedClaimsAsync(10, cancellationToken);
                            claims.AddRange(rejectedClaims);
                        }
                        else
                        {
                            var recentClaims = await _claimService.GetRecentClaimsAsync(20, cancellationToken);
                            claims.AddRange(recentClaims);
                        }
                    }
                }

                if (intent.IncludeVehicles)
                {
                    if (intent.UserId.HasValue)
                    {
                        var userVehicles = await _adminVehicleService.GetVehiclesByUserIdAsync(intent.UserId.Value, cancellationToken);
                        vehicles.AddRange(userVehicles);
                    }
                    else
                    {
                        var recentVehicles = await _adminVehicleService.GetRecentVehiclesAsync(100, cancellationToken);
                        vehicles.AddRange(recentVehicles);
                    }
                }

                if (intent.IncludePayments)
                {
                    paymentAggregates = await _adminPaymentService.GetPaymentAggregatesAsync(cancellationToken);

                    if (intent.PolicyId.HasValue)
                    {
                        var policyPayments = await _adminPaymentService.GetPaymentsByPolicyIdAsync(intent.PolicyId.Value, cancellationToken);
                        payments.AddRange(policyPayments);
                    }
                    else if (intent.UserId.HasValue)
                    {
                        var userPayments = await _adminPaymentService.GetPaymentsByUserIdAsync(intent.UserId.Value, cancellationToken);
                        payments.AddRange(userPayments);
                    }
                    else
                    {
                        var recentPayments = await _adminPaymentService.GetRecentPaymentsAsync(100, cancellationToken);
                        payments.AddRange(recentPayments);
                    }
                }

                if (intent.IncludeApplications)
                {
                    if (intent.UserId.HasValue)
                    {
                        var userApplications = await _adminVehicleApplicationService.GetApplicationsByUserIdAsync(intent.UserId.Value, cancellationToken);
                        applications.AddRange(userApplications);
                    }
                    else
                    {
                        var recentApplications = await _adminVehicleApplicationService.GetRecentApplicationsAsync(100, cancellationToken);
                        applications.AddRange(recentApplications);
                    }
                }

                if (intent.IncludeGarages)
                {
                    var allGarages = await _adminGarageService.GetAllGaragesAsync(cancellationToken);
                    garages.AddRange(allGarages);
                }

                if (intent.IncludeNotifications)
                {
                    if (intent.UserId.HasValue)
                    {
                        var userNotifications = await _adminNotificationService.GetNotificationsByUserIdAsync(intent.UserId.Value, cancellationToken);
                        notifications.AddRange(userNotifications);
                    }
                    else
                    {
                        var recentNotifications = await _adminNotificationService.GetRecentNotificationsAsync(200, cancellationToken);
                        notifications.AddRange(recentNotifications);
                    }
                }

                if (intent.IncludeUsers)
                {
                    if (intent.UserId.HasValue)
                    {
                        var user = await _userService.GetUserByIdAsync(intent.UserId.Value, cancellationToken);
                        if (user != null)
                        {
                            users.Add(user);
                        }
                    }
                    else
                    {
                        if (ContainsAny(lowerQuestion, "claims officer", "claims officers", "claims-officer", "claim officer"))
                        {
                            var claimsOfficers = await _userService.GetUsersByRoleAsync(UserRole.ClaimsOfficer, 500, cancellationToken);
                            users.AddRange(claimsOfficers);
                        }
                        else if (ContainsAny(lowerQuestion, "agent", "agents"))
                        {
                            var agents = await _userService.GetUsersByRoleAsync(UserRole.Agent, 500, cancellationToken);
                            users.AddRange(agents);
                        }
                        else if (ContainsAny(lowerQuestion, "customer", "customers"))
                        {
                            var customers = await _userService.GetUsersByRoleAsync(UserRole.Customer, 500, cancellationToken);
                            users.AddRange(customers);
                        }
                        else
                        {
                            var recentUsers = await _userService.GetRecentUsersAsync(200, cancellationToken);
                            users.AddRange(recentUsers);
                        }
                    }
                }

                if (intent.IncludePolicies)
                {
                    if (intent.PolicyId.HasValue)
                    {
                        var policy = await _policyService.GetPolicyByIdAsync(intent.PolicyId.Value, cancellationToken);
                        if (policy != null)
                        {
                            policies.Add(policy);
                        }
                    }
                    else if (intent.UserId.HasValue)
                    {
                        var userPolicies = await _policyService.GetPoliciesByUserIdAsync(intent.UserId.Value, cancellationToken);
                        policies.AddRange(userPolicies);
                    }
                    else
                    {
                        var recentPolicies = await _policyService.GetRecentPoliciesAsync(20, cancellationToken);
                        policies.AddRange(recentPolicies);
                    }
                }

                if (intent.IncludeReferrals)
                {
                    var allReferrals = await _referralService.GetAllReferralsAsync(cancellationToken);
                    referrals.AddRange(allReferrals);

                    var abuseSignals = await _referralService.GetReferralAbuseSignalsAsync(cancellationToken);
                    referralAbuseSignals.AddRange(abuseSignals);
                }

                if (intent.IncludeReferrals && referralAbuseSignals.Count == 0)
                {
                    var abuseFromUserService = await _userService.GetPotentialReferralAbuseUsersAsync(cancellationToken);
                    referralAbuseSignals.AddRange(abuseFromUserService);
                }

                var rules = await _ragService.SearchAsync(question, cancellationToken);
                var context = _contextBuilder.Build(
                    intent,
                    claims,
                    users,
                    policies,
                    vehicles,
                    payments,
                    paymentAggregates,
                    applications,
                    garages,
                    notifications,
                    referrals,
                    referralAbuseSignals,
                    rules);

                var prompt = _promptBuilder.Build(question, context);
                var llmResponse = await _llmService.GenerateAsync(prompt, cancellationToken);

                var finalRulesApplied = llmResponse.RulesApplied.Count > 0
                    ? llmResponse.RulesApplied
                    : context.RulesUsed;

                _logger.LogInformation(
                    "Admin chat completed by {Provider}. Rules used: {RulesCount}",
                    _llmService.LastProvider,
                    finalRulesApplied.Count);

                return new ChatResponseDto
                {
                    Answer = llmResponse.Answer,
                    Reasoning = llmResponse.Reasoning,
                    RulesApplied = finalRulesApplied,
                    DataUsed = llmResponse.DataUsed.Count > 0 ? llmResponse.DataUsed : context.DataUsed,
                    Confidence = llmResponse.Confidence
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin chat orchestration failed for question: {Question}", question);
                return new ChatResponseDto
                {
                    Answer = "Insufficient data to answer.",
                    Reasoning = "The assistant could not process this query due to an internal error.",
                    Confidence = "LOW",
                    RulesApplied = new List<string>(),
                    DataUsed = new List<string>()
                };
            }
        }
        private static bool ContainsAny(string input, params string[] values)
        {
            return values.Any(input.Contains);
        }
    }
}