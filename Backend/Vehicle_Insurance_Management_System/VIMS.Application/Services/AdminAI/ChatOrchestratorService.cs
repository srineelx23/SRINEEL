using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.DTOs;
using VIMS.Domain.Enums;

namespace VIMS.Application.Services.AdminAI
{
    public class ChatOrchestratorService : IChatOrchestratorService
    {
        private const int MaxRelevantRecords = 5;
        private const int DefaultExpiryRangeDays = 30;
        private const int PreferredContextCharLimit = 1200;

        private static readonly JsonSerializerOptions MinimalContextJsonOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly Regex FollowUpPronounRegex = new(@"\b(this|that|it|them)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IIntentParser _intentParser;
        private readonly IAdminChatSessionMemoryService _sessionMemoryService;
        private readonly IContextBuilder _contextBuilder;
        private readonly IBusinessRuleEvaluatorService _businessRuleEvaluatorService;
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
            IAdminChatSessionMemoryService sessionMemoryService,
            IContextBuilder contextBuilder,
            IBusinessRuleEvaluatorService businessRuleEvaluatorService,
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
            _sessionMemoryService = sessionMemoryService;
            _contextBuilder = contextBuilder;
            _businessRuleEvaluatorService = businessRuleEvaluatorService;
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
                return BuildInsufficient("Admin question is empty.");
            }

            _logger.LogInformation("Admin chat question received: {Question}", question);

            try
            {
                var history = (request?.History ?? new List<string>())
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .TakeLast(4)
                    .ToList();

                var sessionId = NormalizeSessionId(request?.SessionId);
                var sessionMemory = _sessionMemoryService.Get(sessionId);
                var queryType = ClassifyQueryType(question);

                if (IsExplicitlyUnsupportedQuery(question))
                {
                    return BuildInsufficient("This analytics pattern is not supported by deterministic plans yet.");
                }

                var isClaimAnalyticsQuery = IsClaimAnalyticsQuery(question);
                var hasRankingIntent = HasRankingIntent(question);
                var hasClaimRankingIntent = HasClaimRankingIntent(question);
                var hasPolicyRankingIntent = HasPolicyRankingIntent(question);

                var intent = _intentParser.Parse(question, history, sessionMemory);
                var plan = BuildQueryPlan(question, intent, sessionMemory);

                if (queryType != QueryType.Knowledge && plan.PlanType == QueryPlanType.Unmapped && !isClaimAnalyticsQuery)
                {
                    _logger.LogWarning("No deterministic query plan for question: {Question}", question);
                    return BuildInsufficient("No deterministic query plan available for the requested query.");
                }

                var result = new QueryExecutionResult();
                if (queryType != QueryType.Knowledge && !isClaimAnalyticsQuery)
                {
                    result = await ExecuteQueryPlanAsync(plan, intent, cancellationToken);
                    if (IsRequiredDataMissing(plan, result))
                    {
                        return BuildInsufficient($"Required data was not found for plan {plan.PlanType}.");
                    }
                }

                if (isClaimAnalyticsQuery)
                {
                    var analyticsClaims = await _claimService.GetClaimsForAnalyticsAsync(cancellationToken);
                    result.Claims.AddRange(analyticsClaims);

                    var computedClaimAnalytics = ComputeDeterministicMetrics(Array.Empty<PolicyContextDto>())
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    AddClaimAnalyticsComputedValues(computedClaimAnalytics, result.Claims);
                    return BuildClaimAnalyticsDeterministicResponse(computedClaimAnalytics, question);
                }

                if (queryType == QueryType.Analytics && result.PaymentAggregates == null)
                {
                    result.PaymentAggregates = await _adminPaymentService.GetPaymentAggregatesAsync(cancellationToken);
                }

                if (hasClaimRankingIntent && result.Claims.Count == 0)
                {
                    var rankingClaims = await _claimService.GetClaimsForAnalyticsAsync(cancellationToken);
                    result.Claims.AddRange(rankingClaims);
                    result.TotalClaims = result.Claims.Count;
                }

                var filters = ExtractFilters(question);
                var filteredPolicies = result.Policies
                    .Where(p =>
                        (filters.VehicleType == null || MatchesVehicleType(p.VehicleType, filters.VehicleType)) &&
                        (filters.FuelType == null || MatchesFuelType(p.FuelType, filters.FuelType)))
                    .ToList();

                var policiesForComputation = filters.VehicleType == null && filters.FuelType == null
                    ? result.Policies
                    : filteredPolicies;

                var computed = ComputeDeterministicMetrics(policiesForComputation, result.TotalPremiumOverride)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                if (!string.IsNullOrWhiteSpace(filters.VehicleType))
                {
                    computed["appliedVehicleType"] = filters.VehicleType;
                }

                if (!string.IsNullOrWhiteSpace(filters.FuelType))
                {
                    computed["appliedFuelType"] = filters.FuelType;
                }

                if (hasRankingIntent)
                {
                    var rankingLimit = ResolveRankingLimit(question);

                    if (hasPolicyRankingIntent)
                    {
                        var rankedPolicies = policiesForComputation
                            .OrderByDescending(p => p.IDV)
                            .Take(rankingLimit)
                            .Select(p => new
                            {
                                p.PolicyId,
                                p.IDV,
                                p.PremiumAmount,
                                p.VehicleRegistrationNumber,
                                p.VehicleType,
                                p.FuelType
                            })
                            .ToList();

                        computed["topPolicy"] = rankingLimit == 1
                            ? rankedPolicies.FirstOrDefault()
                            : rankedPolicies;
                    }

                    if (hasClaimRankingIntent)
                    {
                        var applyClaimFilter = filters.VehicleType != null || filters.FuelType != null;
                        IEnumerable<ClaimContextDto> claimsForRanking = result.Claims;

                        if (applyClaimFilter)
                        {
                            var policyById = policiesForComputation
                                .ToDictionary(p => p.PolicyId, p => p);

                            var missingPolicyIds = result.Claims
                                .Select(c => c.PolicyId)
                                .Distinct()
                                .Where(policyId => !policyById.ContainsKey(policyId))
                                .ToList();

                            foreach (var policyId in missingPolicyIds)
                            {
                                var policy = await _policyService.GetPolicyByIdAsync(policyId, cancellationToken);
                                if (policy != null)
                                {
                                    policyById[policyId] = policy;
                                }
                            }

                            claimsForRanking = result.Claims.Where(c =>
                                policyById.TryGetValue(c.PolicyId, out var policy) &&
                                (filters.VehicleType == null || MatchesVehicleType(policy.VehicleType, filters.VehicleType)) &&
                                (filters.FuelType == null || MatchesFuelType(policy.FuelType, filters.FuelType)));
                        }

                        var rankedClaimVehicles = claimsForRanking
                            .GroupBy(c => c.PolicyId)
                            .Select(g => new
                            {
                                PolicyId = g.Key,
                                Count = g.Count()
                            })
                            .OrderByDescending(x => x.Count)
                            .ThenBy(x => x.PolicyId)
                            .Take(rankingLimit)
                            .ToList();

                        computed["topClaimVehicle"] = rankingLimit == 1
                            ? rankedClaimVehicles.FirstOrDefault()
                            : rankedClaimVehicles;
                    }
                }

                var rules = new List<string>();
                if (queryType == QueryType.Rule && ShouldUseRag(question) && IsRagIntentAligned(intent.IntentType, plan))
                {
                    rules = await _ragService.SearchAsync(question, intent.IntentType, cancellationToken);
                }
                rules = rules.Take(3).ToList();

                var precomputedAnalysis = _businessRuleEvaluatorService.Evaluate(
                    intent,
                    result.Claims,
                    policiesForComputation,
                    result.Payments,
                    result.PaymentAggregates);

                var context = BuildMinimalContextData(plan, result, policiesForComputation, computed, precomputedAnalysis, rules);

                var totalPoliciesForComputation = policiesForComputation.Count;
                var violationCodes = precomputedAnalysis.Violations
                    .Select(v => v.Code)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList();

                var precomputedPayload = new
                {
                    computed = BuildCoreComputedForLlm(computed),
                    topResult = BuildTopResultForLlm(computed),
                    summary = new
                    {
                        totalClaims = result.TotalClaims,
                        totalPolicies = totalPoliciesForComputation,
                        eligibility = precomputedAnalysis.Eligibility,
                        violations = violationCodes
                    }
                };

                var precomputedAnalysisJson = JsonSerializer.Serialize(precomputedPayload, MinimalContextJsonOptions);

                var deterministicFallback = BuildDeterministicFallback(plan, result, computed);
                var forceDeterministic = ShouldForceDeterministicResponse(plan, queryType, isClaimAnalyticsQuery, hasRankingIntent, filters);
                ChatResponseDto finalResponse;

                if (forceDeterministic)
                {
                    finalResponse = ApplyContextMetadata(deterministicFallback, context);
                }
                else
                {
                    var prompt = _promptBuilder.Build(question, context, precomputedAnalysisJson, history);
                    var llmResponse = await _llmService.GenerateAsync(prompt, cancellationToken);
                    finalResponse = MergeResponses(llmResponse, deterministicFallback, context);
                }

                PersistSessionMemory(sessionId, intent, plan, result);

                _logger.LogInformation(
                    "Admin chat completed by {Provider}. QueryType: {QueryType}. Plan: {PlanType}. Rules used: {RulesCount}",
                    _llmService.LastProvider,
                    queryType,
                    plan.PlanType,
                    finalResponse.RulesApplied.Count);

                return finalResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin chat orchestration failed for question: {Question}", question);
                return BuildInsufficient("The assistant could not process this query due to an internal error.");
            }
        }

        private async Task<QueryExecutionResult> ExecuteQueryPlanAsync(QueryPlan plan, IntentResultDto intent, CancellationToken cancellationToken)
        {
            var result = new QueryExecutionResult();
            var lowerQuestion = plan.NormalizedQuestion;

            switch (plan.PlanType)
            {
                case QueryPlanType.RejectedClaimsToday:
                {
                    var rejectedToday = await _claimService.GetRejectedClaimsForDateAsync(DateTime.UtcNow.Date, MaxRelevantRecords, cancellationToken);
                    result.Claims.AddRange(rejectedToday);
                    result.RejectedSummary = result.Claims
                        .Where(c => string.Equals(c.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
                        .GroupBy(c => c.RejectionReason ?? "Unknown")
                        .Select(g => new RejectedSummaryItem
                        {
                            Reason = g.Key ?? "Unknown",
                            Count = g.Count()
                        })
                        .ToList();

                    result.TotalClaims = result.Claims.Count;

                    var policyIds = rejectedToday.Select(c => c.PolicyId).Distinct().Take(MaxRelevantRecords).ToList();
                    foreach (var policyId in policyIds)
                    {
                        var policy = await _policyService.GetPolicyByIdAsync(policyId, cancellationToken);
                        if (policy != null)
                        {
                            result.Policies.Add(policy);
                        }
                    }
                    break;
                }
                case QueryPlanType.ExpiringPolicies:
                {
                    var from = DateTime.UtcNow.Date;
                    var to = from.AddDays(plan.RangeDays);
                    var expiring = await _policyService.GetPoliciesExpiringInRangeAsync(from, to, MaxRelevantRecords, cancellationToken);
                    result.Policies.AddRange(expiring);
                    break;
                }
                case QueryPlanType.PendingPaymentPolicies:
                {
                    var pending = await _policyService.GetPendingPaymentPoliciesAsync(MaxRelevantRecords, cancellationToken);
                    result.Policies.AddRange(ApplyCap(pending));
                    result.PaymentAggregates = await _adminPaymentService.GetPaymentAggregatesAsync(cancellationToken);
                    break;
                }
                case QueryPlanType.HighestIdvPolicy:
                {
                    var hasFilters = !string.IsNullOrWhiteSpace(plan.VehicleTypeFilter)
                        || !string.IsNullOrWhiteSpace(plan.FuelTypeFilter)
                        || !string.IsNullOrWhiteSpace(plan.PlanTypeFilter);

                    var highest = hasFilters
                        ? await _policyService.GetPolicyWithHighestIdvByFiltersAsync(plan.VehicleTypeFilter, plan.FuelTypeFilter, plan.PlanTypeFilter, cancellationToken)
                        : await _policyService.GetPolicyWithHighestIdvAsync(cancellationToken);

                    if (highest == null && hasFilters)
                    {
                        highest = await _policyService.GetPolicyWithHighestIdvByFiltersAsync(
                            string.Empty,
                            plan.FuelTypeFilter,
                            plan.PlanTypeFilter,
                            cancellationToken);
                    }

                    if (highest != null)
                    {
                        result.Policies.Add(highest);
                    }
                    break;
                }
                case QueryPlanType.PolicyPremiumById:
                {
                    if (!plan.PolicyId.HasValue)
                    {
                        break;
                    }

                    var policy = await _policyService.GetPolicyByIdAsync(plan.PolicyId.Value, cancellationToken);
                    if (policy != null)
                    {
                        result.Policies.Add(policy);
                        result.TotalPremiumOverride = policy.PremiumAmount;
                    }
                    break;
                }
                case QueryPlanType.TotalPremium:
                {
                    result.TotalPremiumOverride = await _adminPaymentService.GetTotalPaymentAmountAsync(null, intent.UserId, cancellationToken);
                    result.PaymentAggregates = await _adminPaymentService.GetPaymentAggregatesAsync(cancellationToken);
                    break;
                }
                case QueryPlanType.TotalPremiumCollected:
                {
                    result.TotalPremiumOverride = await _adminPaymentService.GetTotalPaymentAmountAsync(PaymentStatus.Paid, intent.UserId, cancellationToken);
                    result.PaymentAggregates = await _adminPaymentService.GetPaymentAggregatesAsync(cancellationToken);
                    break;
                }
                case QueryPlanType.TotalPremiumPending:
                {
                    result.TotalPremiumOverride = await _adminPaymentService.GetTotalPaymentAmountAsync(PaymentStatus.Pending, intent.UserId, cancellationToken);
                    result.PaymentAggregates = await _adminPaymentService.GetPaymentAggregatesAsync(cancellationToken);
                    break;
                }
                case QueryPlanType.ZeroDepreciationPolicies:
                {
                    var zeroDep = await _policyService.GetZeroDepreciationPoliciesWithVehiclesAsync(MaxRelevantRecords, cancellationToken);
                    result.Policies.AddRange(zeroDep);
                    result.Vehicles.AddRange(zeroDep
                        .GroupBy(p => p.VehicleId)
                        .Select(g => new VehicleContextDto
                        {
                            VehicleId = g.Key,
                            RegistrationNumber = g.First().VehicleRegistrationNumber,
                            Make = g.First().VehicleMake,
                            Model = g.First().VehicleModel,
                            Year = g.First().VehicleYear,
                            CustomerId = g.First().CustomerId
                        }));
                    break;
                }
                case QueryPlanType.VehicleRegistrationFromPolicy:
                case QueryPlanType.PolicyById:
                {
                    if (!plan.PolicyId.HasValue)
                    {
                        break;
                    }

                    var policy = await _policyService.GetPolicyByIdAsync(plan.PolicyId.Value, cancellationToken);
                    if (policy != null)
                    {
                        result.Policies.Add(policy);
                    }
                    break;
                }
                case QueryPlanType.ClaimById:
                {
                    if (!plan.ClaimId.HasValue)
                    {
                        break;
                    }

                    var claim = await _claimService.GetClaimByIdAsync(plan.ClaimId.Value, cancellationToken);
                    if (claim != null)
                    {
                        result.Claims.Add(claim);

                        var policy = await _policyService.GetPolicyByIdAsync(claim.PolicyId, cancellationToken);
                        if (policy != null)
                        {
                            result.Policies.Add(policy);
                        }
                    }
                    break;
                }
                case QueryPlanType.ClaimByNumber:
                {
                    if (string.IsNullOrWhiteSpace(plan.ClaimNumber))
                    {
                        break;
                    }

                    var claim = await _claimService.GetClaimByNumberAsync(plan.ClaimNumber, cancellationToken);
                    if (claim != null)
                    {
                        result.Claims.Add(claim);

                        var policy = await _policyService.GetPolicyByIdAsync(claim.PolicyId, cancellationToken);
                        if (policy != null)
                        {
                            result.Policies.Add(policy);
                        }
                    }
                    break;
                }
                case QueryPlanType.RejectedClaims:
                {
                    var rejectedClaims = await _claimService.GetRecentRejectedClaimsAsync(MaxRelevantRecords, cancellationToken);
                    result.Claims.AddRange(rejectedClaims);
                    result.RejectedSummary = result.Claims
                        .Where(c => string.Equals(c.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
                        .GroupBy(c => c.RejectionReason ?? "Unknown")
                        .Select(g => new RejectedSummaryItem
                        {
                            Reason = g.Key ?? "Unknown",
                            Count = g.Count()
                        })
                        .ToList();

                    result.TotalClaims = result.Claims.Count;

                    var policyIds = rejectedClaims.Select(c => c.PolicyId).Distinct().Take(MaxRelevantRecords).ToList();
                    foreach (var policyId in policyIds)
                    {
                        var policy = await _policyService.GetPolicyByIdAsync(policyId, cancellationToken);
                        if (policy != null)
                        {
                            result.Policies.Add(policy);
                        }
                    }
                    break;
                }
                case QueryPlanType.UserWithMostClaims:
                {
                    var topUser = await _claimService.GetUserWithMostClaimsAsync(cancellationToken);
                    if (topUser.HasValue)
                    {
                        result.TopClaimCount = topUser.Value.ClaimCount;
                        result.TopUserId = topUser.Value.UserId;

                        var user = await _userService.GetUserByIdAsync(topUser.Value.UserId, cancellationToken);
                        if (user != null)
                        {
                            result.Users.Add(user);
                        }
                    }
                    break;
                }
                case QueryPlanType.VehicleWithMostClaims:
                {
                    var topPolicy = await _claimService.GetPolicyWithMostClaimsAsync(cancellationToken);
                    if (topPolicy.HasValue)
                    {
                        result.TopClaimCount = topPolicy.Value.ClaimCount;
                        result.TopPolicyId = topPolicy.Value.PolicyId;

                        var policy = await _policyService.GetPolicyByIdAsync(topPolicy.Value.PolicyId, cancellationToken);
                        if (policy != null)
                        {
                            result.Policies.Add(policy);
                        }
                    }
                    break;
                }
                case QueryPlanType.VehicleTypesByClaims:
                {
                    var claims = await _claimService.GetClaimsForAnalyticsAsync(cancellationToken);
                    result.Claims.AddRange(claims);
                    result.TotalClaims = result.Claims.Count;

                    var policyIds = result.Claims
                        .Select(c => c.PolicyId)
                        .Distinct()
                        .Take(MaxRelevantRecords * 10)
                        .ToList();

                    foreach (var policyId in policyIds)
                    {
                        var policy = await _policyService.GetPolicyByIdAsync(policyId, cancellationToken);
                        if (policy != null)
                        {
                            result.Policies.Add(policy);
                        }
                    }

                    break;
                }
                case QueryPlanType.HighestClaimPayout:
                {
                    var highestPayoutClaim = await _claimService.GetHighestPayoutClaimAsync(cancellationToken);
                    if (highestPayoutClaim != null)
                    {
                        result.Claims.Add(highestPayoutClaim);

                        var policy = await _policyService.GetPolicyByIdAsync(highestPayoutClaim.PolicyId, cancellationToken);
                        if (policy != null)
                        {
                            result.Policies.Add(policy);
                        }
                    }
                    break;
                }
                case QueryPlanType.GaragesList:
                {
                    break;
                }
                case QueryPlanType.UserPolicies:
                {
                    if (!plan.UserId.HasValue)
                    {
                        break;
                    }

                    var userPolicies = await _policyService.GetPoliciesByUserIdAsync(plan.UserId.Value, cancellationToken);
                    result.Policies.AddRange(ApplyCap(userPolicies));
                    break;
                }
                case QueryPlanType.UserClaims:
                {
                    if (!plan.UserId.HasValue)
                    {
                        break;
                    }

                    var userClaims = await _claimService.GetClaimsByUserIdAsync(plan.UserId.Value, cancellationToken);
                    result.Claims.AddRange(ApplyCap(userClaims));
                    break;
                }
                case QueryPlanType.ReferralReview:
                {
                    var referrals = await _referralService.GetAllReferralsAsync(cancellationToken);
                    var filteredReferrals = plan.RangeDays > 0
                        ? referrals.Where(r => r.CreatedAt >= DateTime.UtcNow.AddDays(-plan.RangeDays)).ToList()
                        : referrals.ToList();

                    result.Referrals.AddRange(ApplyCap(filteredReferrals));

                    var abuseSignals = await _referralService.GetReferralAbuseSignalsAsync(cancellationToken);
                    result.ReferralAbuseSignals.AddRange(ApplyCap(abuseSignals));

                    if (result.ReferralAbuseSignals.Count == 0)
                    {
                        var abuseFromUserService = await _userService.GetPotentialReferralAbuseUsersAsync(cancellationToken);
                        result.ReferralAbuseSignals.AddRange(ApplyCap(abuseFromUserService));
                    }
                    break;
                }
                case QueryPlanType.UserVehicles:
                {
                    if (!plan.UserId.HasValue)
                    {
                        break;
                    }

                    var userVehicles = await _adminVehicleService.GetVehiclesByUserIdAsync(plan.UserId.Value, cancellationToken);
                    result.Vehicles.AddRange(ApplyCap(userVehicles));
                    break;
                }
                case QueryPlanType.RecentUsers:
                {
                    var users = await _userService.GetRecentUsersAsync(MaxRelevantRecords, cancellationToken);
                    result.Users.AddRange(ApplyCap(users));
                    break;
                }
                case QueryPlanType.RecentVehicles:
                {
                    var vehicles = await _adminVehicleService.GetRelevantVehiclesAsync(MaxRelevantRecords, cancellationToken);
                    result.Vehicles.AddRange(ApplyCap(vehicles));
                    break;
                }
                case QueryPlanType.RecentApplications:
                {
                    var applications = await _adminVehicleApplicationService.GetRecentApplicationsAsync(MaxRelevantRecords, cancellationToken);
                    result.Applications.AddRange(ApplyCap(applications));
                    break;
                }
                case QueryPlanType.RecentNotifications:
                {
                    var notifications = await _adminNotificationService.GetRecentNotificationsAsync(MaxRelevantRecords, cancellationToken);
                    result.Notifications.AddRange(ApplyCap(notifications));
                    break;
                }
            }

            if (plan.IncludePayments && result.PaymentAggregates == null)
            {
                result.PaymentAggregates = await _adminPaymentService.GetPaymentAggregatesAsync(cancellationToken);
            }

            if (plan.IncludePayments && plan.PolicyId.HasValue)
            {
                var policyPayments = await _adminPaymentService.GetPaymentsByPolicyIdAsync(plan.PolicyId.Value, cancellationToken);
                result.Payments.AddRange(ApplyCap(policyPayments));
            }
            else if (plan.IncludePayments && plan.UserId.HasValue)
            {
                var userPayments = await _adminPaymentService.GetPaymentsByUserIdAsync(plan.UserId.Value, cancellationToken);
                result.Payments.AddRange(ApplyCap(userPayments));
            }

            if (plan.IncludeUsers && plan.UserId.HasValue)
            {
                var user = await _userService.GetUserByIdAsync(plan.UserId.Value, cancellationToken);
                if (user != null)
                {
                    result.Users.Add(user);
                }
            }

            if (plan.IncludeApplications && plan.UserId.HasValue)
            {
                var userApplications = await _adminVehicleApplicationService.GetApplicationsByUserIdAsync(plan.UserId.Value, cancellationToken);
                result.Applications.AddRange(ApplyCap(userApplications));
            }

            if (plan.IncludeNotifications && plan.UserId.HasValue)
            {
                var userNotifications = await _adminNotificationService.GetNotificationsByUserIdAsync(plan.UserId.Value, cancellationToken);
                result.Notifications.AddRange(ApplyCap(userNotifications));
            }

            if (plan.IncludeGarages)
            {
                var garages = await _adminGarageService.GetAllGaragesAsync(cancellationToken);
                result.Garages.AddRange(ApplyCap(garages));
            }

            // Deterministic analytics are computed before LLM usage.
            result.TotalClaims = result.Claims.Count;
            result.TotalPolicies = result.Policies.Count;

            if ((plan.PlanType == QueryPlanType.RejectedClaimsToday || plan.PlanType == QueryPlanType.RejectedClaims)
                && result.Claims.Count > 0
                && result.RejectedSummary.Count == 0)
            {
                result.RejectedSummary = result.Claims
                    .Where(c => string.Equals(c.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(c => c.RejectionReason ?? "Unknown")
                    .Select(g => new RejectedSummaryItem
                    {
                        Reason = g.Key ?? "Unknown",
                        Count = g.Count()
                    })
                    .ToList();
            }

            return result;
        }

        private QueryPlan BuildQueryPlan(string question, IntentResultDto intent, ContextMemoryDto sessionMemory)
        {
            var lower = question.Trim().ToLowerInvariant();
            var extractedClaimId = ExtractClaimId(question);
            var extractedClaimNumber = ExtractClaimNumber(question);
            var effectiveClaimId = intent.ClaimId ?? extractedClaimId;
            var effectiveClaimNumber = extractedClaimNumber;
            var useMemoryForThisQuery = ShouldUseMemoryForQuery(lower);
            var effectivePolicyId = intent.PolicyId ?? (useMemoryForThisQuery ? sessionMemory.AnchorPolicyId : null);
            var rangeDays = NormalizeRange(intent.RequestedRangeDays ?? sessionMemory.LastRangeDays);

            if (ContainsAny(lower, "rejected", "rejection") && ContainsAny(lower, "today") && ContainsAny(lower, "claim", "claims"))
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.RejectedClaimsToday,
                    IncludeClaims = true,
                    IncludePolicies = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "rejectedClaimsToday"
                };
            }

            if (ContainsAny(lower, "garage", "garages", "workshop", "workshops") && ContainsAny(lower, "list", "show", "all"))
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.GaragesList,
                    IncludeGarages = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "garages"
                };
            }

            if (ContainsAny(lower, "most", "highest", "max", "maximum", "top") && ContainsAny(lower, "claim", "claims") && ContainsAny(lower, "user", "customer"))
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.UserWithMostClaims,
                    IncludeUsers = true,
                    IncludeClaims = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "userWithMostClaims"
                };
            }

            if (ContainsAny(lower, "most", "highest", "max", "maximum", "top") && ContainsAny(lower, "claim", "claims") && ContainsAny(lower, "vehicle", "car", "registration"))
            {
                if (ContainsAny(lower, "vehicle type", "vehicle types"))
                {
                    return new QueryPlan
                    {
                        PlanType = QueryPlanType.VehicleTypesByClaims,
                        IncludePolicies = true,
                        IncludeClaims = true,
                        RequireRag = intent.RequiresExplanation,
                        NormalizedQuestion = lower,
                        RequiredDataHint = "vehicleTypesByClaims"
                    };
                }

                return new QueryPlan
                {
                    PlanType = QueryPlanType.VehicleWithMostClaims,
                    IncludePolicies = true,
                    IncludeClaims = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "vehicleWithMostClaims"
                };
            }

            if (ContainsAny(lower, "highest claim payout", "max claim payout", "maximum claim payout") || (ContainsAny(lower, "highest", "max", "maximum", "top") && ContainsAny(lower, "claim") && ContainsAny(lower, "payout", "approved amount", "settlement")))
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.HighestClaimPayout,
                    IncludeClaims = true,
                    IncludePolicies = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "highestClaimPayout"
                };
            }

            if (ContainsAny(lower, "claim", "claims") && !string.IsNullOrWhiteSpace(effectiveClaimNumber))
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.ClaimByNumber,
                    ClaimNumber = effectiveClaimNumber,
                    IncludeClaims = true,
                    IncludePolicies = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "claimByNumber"
                };
            }

            if (ContainsAny(lower, "premium") && useMemoryForThisQuery && effectivePolicyId.HasValue)
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.PolicyPremiumById,
                    PolicyId = effectivePolicyId,
                    IncludePolicies = true,
                    RequireRag = false,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "policyPremiumById"
                };
            }

            if (ContainsAny(lower, "what all vehicles have taken zero depreciation policy", "which vehicles have taken zero depreciation", "zero depreciation policy"))
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.ZeroDepreciationPolicies,
                    IncludePolicies = true,
                    IncludeVehicles = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "zeroDepPolicies"
                };
            }

            if (ContainsAny(lower, "registration number", "registration no", "vehicle registration") && effectivePolicyId.HasValue)
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.VehicleRegistrationFromPolicy,
                    PolicyId = effectivePolicyId,
                    IncludePolicies = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "policy.vehicleRegistrationNumber"
                };
            }

            if (ContainsAny(lower, "expiring", "expiry", "expires", "due for renewal"))
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.ExpiringPolicies,
                    RangeDays = rangeDays,
                    IncludePolicies = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "policies"
                };
            }

            if (ContainsAny(lower, "total premium", "sum premium", "total premiums", "premium collected"))
            {
                var totalPlanType = ContainsAny(lower, "collected", "paid")
                    ? QueryPlanType.TotalPremiumCollected
                    : ContainsAny(lower, "pending", "unpaid")
                        ? QueryPlanType.TotalPremiumPending
                        : QueryPlanType.TotalPremium;

                return new QueryPlan
                {
                    PlanType = totalPlanType,
                    UserId = intent.UserId,
                    IncludePayments = true,
                    RequireRag = false,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "premiumTotal"
                };
            }

            if (ContainsAny(lower, "highest idv", "max idv", "maximum idv", "top idv", "highest insured declared value"))
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.HighestIdvPolicy,
                    IncludePolicies = true,
                    RequireRag = intent.RequiresExplanation,
                    VehicleTypeFilter = intent.VehicleTypeFilter,
                    FuelTypeFilter = intent.FuelTypeFilter,
                    PlanTypeFilter = intent.PlanTypeFilter,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "policyWithHighestIdv"
                };
            }

            if (ContainsAny(lower, "pending premium", "pending payment", "unpaid premium", "premium due", "overdue premium"))
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.PendingPaymentPolicies,
                    IncludePolicies = true,
                    IncludePayments = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "pendingPaymentPolicies"
                };
            }

            if (ContainsAny(lower, "rejected", "rejection") && ContainsAny(lower, "claim", "claims"))
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.RejectedClaims,
                    IncludeClaims = true,
                    IncludePolicies = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "rejectedClaims"
                };
            }

            if (intent.IncludeReferrals || ContainsAny(lower, "referral", "referred", "referrer", "referee", "abuse", "suspicious"))
            {
                var referralRangeDays = ContainsAny(lower, "today", "week", "month", "days") ? rangeDays : 0;

                return new QueryPlan
                {
                    PlanType = QueryPlanType.ReferralReview,
                    IncludeReferrals = true,
                    RangeDays = referralRangeDays,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "referrals"
                };
            }

            if (effectiveClaimId.HasValue)
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.ClaimById,
                    ClaimId = effectiveClaimId,
                    IncludeClaims = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "claim"
                };
            }

            if (intent.UserId.HasValue && intent.IncludePolicies)
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.UserPolicies,
                    UserId = intent.UserId,
                    IncludePolicies = true,
                    IncludeUsers = true,
                    IncludePayments = intent.IncludePayments,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "userPolicies"
                };
            }

            if (intent.UserId.HasValue && intent.IncludeClaims)
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.UserClaims,
                    UserId = intent.UserId,
                    IncludeClaims = true,
                    IncludeUsers = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "userClaims"
                };
            }

            if (intent.UserId.HasValue && intent.IncludeVehicles)
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.UserVehicles,
                    UserId = intent.UserId,
                    IncludeVehicles = true,
                    IncludeUsers = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "userVehicles"
                };
            }

            if (intent.IncludeApplications && !intent.UserId.HasValue)
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.RecentApplications,
                    IncludeApplications = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "applications"
                };
            }

            if (intent.IncludeNotifications && !intent.UserId.HasValue)
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.RecentNotifications,
                    IncludeNotifications = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "notifications"
                };
            }

            if (intent.IncludeUsers && !intent.UserId.HasValue)
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.RecentUsers,
                    IncludeUsers = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "users"
                };
            }

            if (intent.IncludeVehicles && !intent.UserId.HasValue)
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.RecentVehicles,
                    IncludeVehicles = true,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "vehicles"
                };
            }

            if (effectivePolicyId.HasValue)
            {
                return new QueryPlan
                {
                    PlanType = QueryPlanType.PolicyById,
                    PolicyId = effectivePolicyId,
                    IncludePolicies = true,
                    IncludePayments = intent.IncludePayments,
                    RequireRag = intent.RequiresExplanation,
                    NormalizedQuestion = lower,
                    RequiredDataHint = "policy"
                };
            }

            return new QueryPlan
            {
                PlanType = QueryPlanType.Unmapped,
                NormalizedQuestion = lower,
                RequiredDataHint = "unmapped"
            };
        }

        private static bool IsRequiredDataMissing(QueryPlan plan, QueryExecutionResult result)
        {
            if (CanReturnEmptyResult(plan.PlanType))
            {
                return false;
            }

            return result.Claims.Count == 0
                && result.Policies.Count == 0
                && result.Users.Count == 0
                && result.Vehicles.Count == 0
                && result.Payments.Count == 0
                && result.PaymentAggregates == null;
        }

        private static bool ShouldUseRag(string question)
        {
            var lower = question.Trim().ToLowerInvariant();
            return ContainsAny(lower, "why", "explain", "reason", "rule");
        }

        private static bool IsClaimAnalyticsQuery(string question)
        {
            var q = (question ?? string.Empty).Trim().ToLowerInvariant();

            if (!ContainsAny(q, "claim", "claims"))
            {
                return false;
            }

            if (ContainsAny(q, "claim payout") && ContainsAny(q, "total", "so far", "overall"))
            {
                return true;
            }

            if (ContainsAny(q, "approved") && ContainsAny(q, "how many", "count", "total", "number"))
            {
                return true;
            }

            if (ContainsAny(q, "made", "filed", "created") && ContainsAny(q, "how many", "count", "total", "number"))
            {
                return true;
            }

            return ContainsAny(
                q,
                "total claim payout",
                "total claims made",
                "total claims have been made",
                "how many total claims",
                "how many claims have been made",
                "how many claims are approved",
                "how many approved claims",
                "claims approved right now",
                "total approved claims",
                "total claims approved");
        }

        private static bool IsExplicitlyUnsupportedQuery(string question)
        {
            var q = (question ?? string.Empty).Trim().ToLowerInvariant();

            var monthlyTrendUnsupported = ContainsAny(q, "month-wise", "month wise", "month-over-month", "mom", "growth percentage");
            var forecastUnsupported = ContainsAny(q, "predict", "forecast", "next quarter", "confidence score");
            var fraudRankingUnsupported = ContainsAny(q, "suspicious customer", "fraud probability", "risk band");
            var branchCityRatioUnsupported = ContainsAny(q, "claim frequency ratio") && ContainsAny(q, "city", "branch");
            var appealsUnsupported = ContainsAny(q, "appeal") || (ContainsAny(q, "rejected") && ContainsAny(q, "payout difference", "later approved"));

            return monthlyTrendUnsupported
                || forecastUnsupported
                || fraudRankingUnsupported
                || branchCityRatioUnsupported
                || appealsUnsupported;
        }

        private static bool HasClaimRankingIntent(string question)
        {
            var q = (question ?? string.Empty).Trim().ToLowerInvariant();
            return ContainsAny(q, "claim", "claims") && ContainsAny(q, "highest", "most", "top", "maximum");
        }

        private static bool HasPolicyRankingIntent(string question)
        {
            var q = (question ?? string.Empty).Trim().ToLowerInvariant();
            return ContainsAny(q, "idv", "insured declared value")
                || (ContainsAny(q, "highest", "most", "top", "maximum") && !ContainsAny(q, "claim", "claims"));
        }

        private static bool HasAppliedFilter(FilterCriteria filters)
        {
            return !string.IsNullOrWhiteSpace(filters.VehicleType) || !string.IsNullOrWhiteSpace(filters.FuelType);
        }

        private static ChatResponseDto ApplyContextMetadata(ChatResponseDto deterministicResponse, ContextDataDto context)
        {
            deterministicResponse.RulesApplied = context.RulesUsed;
            if (deterministicResponse.DataUsed.Count == 0)
            {
                deterministicResponse.DataUsed = context.DataUsed;
            }

            return deterministicResponse;
        }

        private static void AddClaimAnalyticsComputedValues(Dictionary<string, object> computed, IReadOnlyList<ClaimContextDto> claims)
        {
            var totalClaims = claims.Count;
            var totalApprovedClaims = claims.Count(c => string.Equals(c.Status, "Approved", StringComparison.OrdinalIgnoreCase));
            var totalClaimPayout = claims
                .Where(c => string.Equals(c.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                .Sum(c => c.ApprovedAmount ?? 0m);

            computed["TotalClaims"] = totalClaims;
            computed["TotalApprovedClaims"] = totalApprovedClaims;
            computed["TotalClaimPayout"] = totalClaimPayout;
        }

        private static ChatResponseDto BuildClaimAnalyticsDeterministicResponse(IReadOnlyDictionary<string, object> computed, string question)
        {
            var totalClaims = computed.TryGetValue("TotalClaims", out var totalClaimsObj)
                ? Convert.ToInt32(totalClaimsObj)
                : 0;
            var totalApprovedClaims = computed.TryGetValue("TotalApprovedClaims", out var totalApprovedClaimsObj)
                ? Convert.ToInt32(totalApprovedClaimsObj)
                : 0;
            var totalClaimPayout = computed.TryGetValue("TotalClaimPayout", out var totalClaimPayoutObj)
                ? Convert.ToDecimal(totalClaimPayoutObj)
                : 0m;

            var lower = (question ?? string.Empty).Trim().ToLowerInvariant();
            var answer = ContainsAny(lower, "approved") && ContainsAny(lower, "how many", "count", "total", "number")
                ? $"Total approved claims: {totalApprovedClaims}."
                : ContainsAny(lower, "claim payout")
                    ? $"Total claim payout: {totalClaimPayout:0.##}."
                    : ContainsAny(lower, "made", "filed", "created")
                        ? $"Total claims made: {totalClaims}."
                        : $"Total claims made: {totalClaims}. Total claims approved: {totalApprovedClaims}. Total claim payout: {totalClaimPayout:0.##}.";

            return new ChatResponseDto
            {
                Answer = answer,
                Reasoning = "Values were computed deterministically in backend from claims data using total count, approved count, and sum of approved amounts.",
                Confidence = "HIGH",
                RulesApplied = new List<string>(),
                DataUsed = new List<string> { "claims", "computed" }
            };
        }

        private static bool HasRankingIntent(string question)
        {
            var q = (question ?? string.Empty).Trim().ToLowerInvariant();
            return ContainsAny(q, "highest", "most", "top", "maximum");
        }

        private static int ResolveRankingLimit(string question)
        {
            var q = (question ?? string.Empty).Trim().ToLowerInvariant();
            if (ContainsAny(q, "top 3", "top three", "highest 3", "most 3", "maximum 3"))
            {
                return 3;
            }

            return 1;
        }

        private FilterCriteria ExtractFilters(string question)
        {
            var q = (question ?? string.Empty).ToLowerInvariant();

            var hasElectricIntent = q.Contains("electric") || q.Contains("ev");

            return new FilterCriteria
            {
                VehicleType = q.Contains("two wheeler") || q.Contains("two-wheeler") ? "TwoWheeler"
                    : q.Contains("car") ? "Car"
                    : q.Contains("private") ? "Private"
                    : q.Contains("commercial") ? "Commercial"
                    : q.Contains("heavy") ? "HeavyVehicle"
                    : null,

                FuelType = hasElectricIntent ? "Electric"
                    : q.Contains("petrol") ? "Petrol"
                    : q.Contains("diesel") ? "Diesel"
                    : null
            };
        }

        private static bool MatchesVehicleType(string policyVehicleType, string filterVehicleType)
        {
            var policy = NormalizeVehicleType(policyVehicleType);
            var filter = NormalizeVehicleType(filterVehicleType);

            if (filter == "CAR")
            {
                return policy is "CAR" or "FOURWHEELER" or "PRIVATE";
            }

            if (filter == "PRIVATE")
            {
                return policy is "PRIVATE" or "CAR" or "FOURWHEELER";
            }

            return string.Equals(policy, filter, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesFuelType(string policyFuelType, string filterFuelType)
        {
            var policy = (policyFuelType ?? string.Empty).Trim();
            var filter = (filterFuelType ?? string.Empty).Trim();

            if (string.Equals(filter, "Electric", StringComparison.OrdinalIgnoreCase))
            {
                return policy.Contains("electric", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(policy, "EV", StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(policy, filter, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeVehicleType(string value)
        {
            return (value ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Trim();
        }

        private static bool IsRagIntentAligned(string intentType, QueryPlan plan)
        {
            var normalized = (intentType ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized is not ("CLAIM" or "POLICY" or "PAYMENT" or "REFERRAL"))
            {
                return false;
            }

            return plan.PlanType switch
            {
                QueryPlanType.ClaimById or QueryPlanType.ClaimByNumber or QueryPlanType.UserClaims or QueryPlanType.RejectedClaims or QueryPlanType.UserWithMostClaims or QueryPlanType.VehicleWithMostClaims or QueryPlanType.HighestClaimPayout => normalized == "CLAIM",
                QueryPlanType.PendingPaymentPolicies => normalized == "PAYMENT" || normalized == "POLICY",
                QueryPlanType.ReferralReview => normalized == "REFERRAL",
                QueryPlanType.RejectedClaimsToday => normalized == "CLAIM" || normalized == "POLICY",
                QueryPlanType.GaragesList => normalized == "GENERAL",
                QueryPlanType.ExpiringPolicies or QueryPlanType.HighestIdvPolicy or QueryPlanType.PolicyById or QueryPlanType.VehicleRegistrationFromPolicy or QueryPlanType.UserPolicies or QueryPlanType.ZeroDepreciationPolicies => normalized == "POLICY",
                _ => false
            };
        }

        private static IReadOnlyDictionary<string, object> ComputeDeterministicMetrics(IReadOnlyList<PolicyContextDto> policies, decimal? totalPremiumOverride = null)
        {
            var highestIdv = policies.Count == 0 ? 0m : policies.Max(p => p.IDV);
            var totalPremium = totalPremiumOverride ?? policies.Sum(p => p.PremiumAmount);
            var pendingCount = policies.Count(p => IsPendingPaymentStatus(p.Status));

            return new Dictionary<string, object>
            {
                ["highestIDV"] = highestIdv,
                ["totalPremium"] = totalPremium,
                ["pendingCount"] = pendingCount
            };
        }

        private ChatResponseDto BuildDeterministicFallback(QueryPlan plan, QueryExecutionResult result, IReadOnlyDictionary<string, object> computed)
        {
            if (plan.PlanType != QueryPlanType.VehicleTypesByClaims
                && TryBuildRankingAnalyticsFallbackAnswer(computed, out var rankingAnswer, out var rankingReasoning))
            {
                var dataUsed = BuildDataUsed(result);
                if (!dataUsed.Contains("computed"))
                {
                    dataUsed.Add("computed");
                }

                return new ChatResponseDto
                {
                    Answer = rankingAnswer,
                    Reasoning = rankingReasoning,
                    Confidence = "HIGH",
                    RulesApplied = new List<string>(),
                    DataUsed = dataUsed
                };
            }

            string answer;
            string reasoning;

            switch (plan.PlanType)
            {
                case QueryPlanType.RejectedClaimsToday:
                    if (result.RejectedSummary.Any())
                    {
                        answer = $"Total {result.TotalClaims} rejected claims today. Breakdown: " +
                            string.Join(", ", result.RejectedSummary.Select(r => $"{r.Reason}: {r.Count}"));
                    }
                    else
                    {
                        answer = result.Claims.Count == 0
                            ? "No claims were rejected today."
                            : string.Join("; ", result.Claims.Select(c =>
                            {
                                var policyForClaim = result.Policies.FirstOrDefault(p => p.PolicyId == c.PolicyId);
                                var reg = policyForClaim?.VehicleRegistrationNumber ?? "N/A";
                                var reason = string.IsNullOrWhiteSpace(c.RejectionReason) ? "Reason not recorded" : c.RejectionReason;
                                return $"ClaimId {c.ClaimId}, PolicyId {c.PolicyId}, Vehicle {reg}, RejectionReason: {reason}";
                            }));
                    }
                    reasoning = "Claims were filtered deterministically by Rejected status and claim date equal to today.";
                    break;
                case QueryPlanType.RejectedClaims:
                    answer = result.Claims.Count == 0
                        ? "No rejected claims were found in recent records."
                        : string.Join("; ", result.Claims.Select(c =>
                        {
                            var policyForClaim = result.Policies.FirstOrDefault(p => p.PolicyId == c.PolicyId);
                            var reg = policyForClaim?.VehicleRegistrationNumber ?? "N/A";
                            var reason = string.IsNullOrWhiteSpace(c.RejectionReason) ? "Reason not recorded" : c.RejectionReason;
                            return $"Claim {c.ClaimNumber} (ClaimId {c.ClaimId}), PolicyId {c.PolicyId}, Vehicle {reg}, RejectionReason: {reason}";
                        }));
                    reasoning = "Claims were filtered deterministically by Rejected status and sorted by latest creation date.";
                    break;
                case QueryPlanType.GaragesList:
                    answer = result.Garages.Count == 0
                        ? "No garages are currently configured in the system."
                        : string.Join("; ", result.Garages.Select(g => $"GarageId {g.GarageId}: {g.GarageName} (Phone: {g.PhoneNumber})"));
                    reasoning = "Garages were retrieved deterministically from the registered garages catalog.";
                    break;
                case QueryPlanType.UserWithMostClaims:
                    var topUser = result.Users.First();
                    var topUserClaims = result.TopClaimCount ?? 0;
                    answer = $"User {topUser.FullName} (UserId {topUser.UserId}) has the highest number of claims: {topUserClaims}.";
                    reasoning = "Claims were grouped by user and ranked by claim count descending.";
                    break;
                case QueryPlanType.VehicleWithMostClaims:
                    var topVehiclePolicy = result.Policies.First();
                    var topVehicleClaims = result.TopClaimCount ?? 0;
                    answer = $"Vehicle {topVehiclePolicy.VehicleRegistrationNumber} (PolicyId {topVehiclePolicy.PolicyId}) has the highest number of claims: {topVehicleClaims}.";
                    reasoning = "Claims were grouped by policy/vehicle and ranked by claim count descending.";
                    break;
                case QueryPlanType.VehicleTypesByClaims:
                    var policyTypeMap = result.Policies
                        .GroupBy(p => p.PolicyId)
                        .ToDictionary(g => g.Key, g => FormatVehicleTypeLabel(g.First().VehicleType));

                    var topVehicleTypes = result.Claims
                        .Where(c => policyTypeMap.ContainsKey(c.PolicyId))
                        .GroupBy(c => policyTypeMap[c.PolicyId])
                        .Select(g => new { VehicleType = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ThenBy(x => x.VehicleType)
                        .Take(3)
                        .ToList();

                    answer = topVehicleTypes.Count == 0
                        ? "No claim records are available to rank vehicle types."
                        : "Top vehicle types by claims: " + string.Join("; ", topVehicleTypes.Select((x, index) => $"{index + 1}. {x.VehicleType} ({x.Count})")) + ".";
                    reasoning = "Claims were grouped by vehicle type via policy mapping and ranked by count descending.";
                    break;
                case QueryPlanType.HighestClaimPayout:
                    var highestPayoutClaim = result.Claims.First();
                    var payoutPolicy = result.Policies.FirstOrDefault(p => p.PolicyId == highestPayoutClaim.PolicyId);
                    var payoutReg = payoutPolicy?.VehicleRegistrationNumber ?? "N/A";
                    answer = $"Highest claim payout is for Claim {highestPayoutClaim.ClaimNumber} (ClaimId {highestPayoutClaim.ClaimId}), PolicyId {highestPayoutClaim.PolicyId}, vehicle {payoutReg}, amount {(highestPayoutClaim.ApprovedAmount ?? 0m):0.##}.";
                    reasoning = "Claims with approved payout were sorted by ApprovedAmount descending and top 1 was selected.";
                    break;
                case QueryPlanType.ExpiringPolicies:
                    answer = result.Policies.Count == 0
                        ? $"No policies are expiring in the next {plan.RangeDays} days."
                        : string.Join("; ", result.Policies.Select(p => $"PolicyId {p.PolicyId} ({p.PolicyNumber}) vehicle {p.VehicleRegistrationNumber} expires on {p.EndDate:yyyy-MM-dd}"));
                    reasoning = $"Policies were filtered with EndDate between today and today + {plan.RangeDays} days.";
                    break;
                case QueryPlanType.ZeroDepreciationPolicies:
                    answer = result.Policies.Count == 0
                        ? "No vehicles are currently mapped to a zero depreciation policy."
                        : string.Join("; ", result.Policies.Select(p => $"PolicyId {p.PolicyId} vehicle {p.VehicleRegistrationNumber} ({p.VehicleMake} {p.VehicleModel}) plan {p.PlanName}"));
                    reasoning = "Policies were filtered deterministically where zero depreciation is enabled and joined with vehicle details.";
                    break;
                case QueryPlanType.PendingPaymentPolicies:
                    answer = result.Policies.Count == 0
                        ? "No policies are currently in pending payment status."
                        : string.Join("; ", result.Policies.Select(p => $"PolicyId {p.PolicyId} ({p.PolicyNumber}) vehicle {p.VehicleRegistrationNumber} customer {p.CustomerName}"));
                    reasoning = "Policies were filtered deterministically by PendingPayment status.";
                    break;
                case QueryPlanType.HighestIdvPolicy:
                    if (result.Policies.Count == 0)
                    {
                        var filterParts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(plan.VehicleTypeFilter)) filterParts.Add($"vehicleType={plan.VehicleTypeFilter}");
                        if (!string.IsNullOrWhiteSpace(plan.FuelTypeFilter)) filterParts.Add($"fuelType={plan.FuelTypeFilter}");
                        if (!string.IsNullOrWhiteSpace(plan.PlanTypeFilter)) filterParts.Add($"planType={plan.PlanTypeFilter}");

                        var filterText = filterParts.Count == 0
                            ? "within available policy data"
                            : "for filters: " + string.Join(", ", filterParts);

                        answer = $"No matching policy was found {filterText}.";
                        reasoning = "Policy lookup was executed deterministically using requested filters but no records matched.";
                    }
                    else
                    {
                        var highest = result.Policies.First();
                        answer = $"PolicyId {highest.PolicyId} vehicle {highest.VehicleRegistrationNumber} has the highest IDV ({highest.IDV:0.##}) with premium {highest.PremiumAmount:0.##}.";
                        reasoning = "Policy data was filtered first (if filters were provided), then sorted by IDV descending and top 1 was selected.";
                    }
                    break;
                case QueryPlanType.TotalPremium:
                    var totalPremium = computed.TryGetValue("totalPremium", out var totalObj) ? Convert.ToDecimal(totalObj) : result.TotalPremiumOverride ?? 0m;
                    answer = $"Total premium across the selected policy scope (PolicyId: multiple, Vehicle: multiple) is {totalPremium:0.##}.";
                    reasoning = "Total premium was computed deterministically in backend using SUM(all payments).";
                    break;
                case QueryPlanType.TotalPremiumCollected:
                    var collected = computed.TryGetValue("totalPremium", out var collectedObj) ? Convert.ToDecimal(collectedObj) : result.TotalPremiumOverride ?? 0m;
                    answer = $"Total premium collected across the selected policy scope (PolicyId: multiple, Vehicle: multiple) is {collected:0.##}.";
                    reasoning = "Total premium collected was computed deterministically using SUM(payments WHERE status = Paid).";
                    break;
                case QueryPlanType.TotalPremiumPending:
                    var pending = computed.TryGetValue("totalPremium", out var pendingObj) ? Convert.ToDecimal(pendingObj) : result.TotalPremiumOverride ?? 0m;
                    answer = $"Total pending premium across the selected policy scope (PolicyId: multiple, Vehicle: multiple) is {pending:0.##}.";
                    reasoning = "Total pending premium was computed deterministically using SUM(payments WHERE status = Pending).";
                    break;
                case QueryPlanType.PolicyPremiumById:
                    var premiumPolicy = result.Policies.First();
                    answer = $"PolicyId {premiumPolicy.PolicyId} vehicle {premiumPolicy.VehicleRegistrationNumber} has premium amount {premiumPolicy.PremiumAmount:0.##}.";
                    reasoning = "Policy premium was resolved deterministically from the memory-anchored policyId.";
                    break;
                case QueryPlanType.VehicleRegistrationFromPolicy:
                case QueryPlanType.PolicyById:
                    var policy = result.Policies.First();
                    answer = $"PolicyId {policy.PolicyId} vehicle registration number is {policy.VehicleRegistrationNumber}.";
                    reasoning = "Policy was resolved deterministically by policyId and registration was read from associated vehicle data.";
                    break;
                case QueryPlanType.ClaimById:
                case QueryPlanType.ClaimByNumber:
                    var claim = result.Claims.First();
                    var claimPolicy = result.Policies.FirstOrDefault(p => p.PolicyId == claim.PolicyId);
                    var claimReg = claimPolicy?.VehicleRegistrationNumber ?? "N/A";
                    var rejectionReason = string.IsNullOrWhiteSpace(claim.RejectionReason) ? "Reason not recorded" : claim.RejectionReason;
                    answer = string.Equals(claim.Status, "Rejected", StringComparison.OrdinalIgnoreCase)
                        ? $"Claim {claim.ClaimNumber} (ClaimId {claim.ClaimId}) for PolicyId {claim.PolicyId}, vehicle {claimReg}, is Rejected. RejectionReason: {rejectionReason}."
                        : $"Claim {claim.ClaimNumber} (ClaimId {claim.ClaimId}) for PolicyId {claim.PolicyId}, vehicle {claimReg}, is currently {claim.Status}.";
                    reasoning = plan.PlanType == QueryPlanType.ClaimByNumber
                        ? "Claim was retrieved deterministically by claim number with related policy context."
                        : "Claim was retrieved deterministically by claimId with related policy context.";
                    break;
                case QueryPlanType.UserPolicies:
                    answer = result.Policies.Count == 0
                        ? "No policies found for the specified user."
                        : string.Join("; ", result.Policies.Select(p => $"{p.PolicyNumber} ({p.Status})"));
                    reasoning = "Policies were retrieved deterministically by customer userId.";
                    break;
                case QueryPlanType.UserVehicles:
                    answer = result.Vehicles.Count == 0
                        ? "No vehicles found for the specified user."
                        : string.Join("; ", result.Vehicles.Select(v => $"{v.RegistrationNumber} ({v.Make} {v.Model})"));
                    reasoning = "Vehicles were retrieved deterministically by customer userId.";
                    break;
                case QueryPlanType.UserClaims:
                    answer = result.Claims.Count == 0
                        ? "No claims found for the specified user."
                        : string.Join("; ", result.Claims.Select(c => $"{c.ClaimNumber} ({c.Status})"));
                    reasoning = "Claims were retrieved deterministically by customer userId.";
                    break;
                case QueryPlanType.RecentUsers:
                    answer = result.Users.Count == 0
                        ? "No user records are available."
                        : "Recent users: " + string.Join("; ", result.Users.Select(u => $"UserId {u.UserId} {u.FullName} ({u.Role})"));
                    reasoning = "Recent user records were retrieved deterministically from user data.";
                    break;
                case QueryPlanType.RecentVehicles:
                    answer = result.Vehicles.Count == 0
                        ? "No vehicle records are available."
                        : "Recent vehicles: " + string.Join("; ", result.Vehicles.Select(v => $"VehicleId {v.VehicleId} {v.RegistrationNumber} ({v.Make} {v.Model})"));
                    reasoning = "Recent vehicle records were retrieved deterministically from vehicle data.";
                    break;
                case QueryPlanType.RecentApplications:
                    answer = result.Applications.Count == 0
                        ? "No recent vehicle applications were found."
                        : "Recent vehicle applications: " + string.Join("; ", result.Applications.Select(a => $"ApplicationId {a.VehicleApplicationId} {a.RegistrationNumber} ({a.Status})"));
                    reasoning = "Recent vehicle applications were retrieved deterministically from application data.";
                    break;
                case QueryPlanType.RecentNotifications:
                    answer = result.Notifications.Count == 0
                        ? "No recent notifications were found."
                        : "Recent notifications: " + string.Join("; ", result.Notifications.Select(n => $"NotificationId {n.NotificationId} {n.Title} ({n.Type})"));
                    reasoning = "Recent notifications were retrieved deterministically from notification data.";
                    break;
                case QueryPlanType.ReferralReview:
                    if (result.ReferralAbuseSignals.Count == 0)
                    {
                        answer = result.Referrals.Count == 0
                            ? "No suspicious referral patterns were detected in the selected period."
                            : $"No high-risk referral abuse signals detected. Reviewed {result.Referrals.Count} referral records.";
                    }
                    else
                    {
                        var highestRisk = result.ReferralAbuseSignals
                            .OrderByDescending(r => NormalizeRiskLevel(r.RiskLevel))
                            .ThenByDescending(r => r.TotalReferrals)
                            .First();

                        answer = $"Detected {result.ReferralAbuseSignals.Count} referral abuse signal(s). Highest risk user: {highestRisk.UserName} (UserId {highestRisk.UserId}, Risk {highestRisk.RiskLevel}, TotalReferrals {highestRisk.TotalReferrals}).";
                    }
                    reasoning = "Referral records and abuse signals were evaluated deterministically for suspicious patterns.";
                    break;
                default:
                    answer = "Insufficient data to answer.";
                    reasoning = "No deterministic fallback available for this query plan.";
                    break;
            }

            return new ChatResponseDto
            {
                Answer = answer,
                Reasoning = reasoning,
                Confidence = "HIGH",
                RulesApplied = new List<string>(),
                DataUsed = BuildDataUsed(result)
            };
        }

        private static bool TryBuildRankingAnalyticsFallbackAnswer(
            IReadOnlyDictionary<string, object> computed,
            out string answer,
            out string reasoning)
        {
            answer = string.Empty;
            reasoning = string.Empty;

            var parts = new List<string>();

            if (TryExtractTopPolicy(computed, out var registration, out var highestIdv))
            {
                var regDisplay = string.IsNullOrWhiteSpace(registration) ? "N/A" : registration;
                parts.Add($"Vehicle {regDisplay} has highest IDV of {highestIdv:0.##}");
            }

            if (TryExtractTopClaimVehicle(computed, out var topPolicyId, out var claimCount))
            {
                parts.Add($"Vehicle with PolicyId {topPolicyId} has most claims: {claimCount}");
            }

            if (parts.Count == 0)
            {
                return false;
            }

            var filterPrefix = BuildFilterPrefix(computed);
            answer = string.IsNullOrWhiteSpace(filterPrefix)
                ? string.Join(". ", parts) + "."
                : $"{filterPrefix}{string.Join(". ", parts)}.";

            reasoning = "Ranking and filtering were applied deterministically using backend computed analytics values.";
            return true;
        }

        private static bool TryExtractTopPolicy(
            IReadOnlyDictionary<string, object> computed,
            out string registration,
            out decimal idv)
        {
            registration = string.Empty;
            idv = 0m;

            if (!computed.TryGetValue("topPolicy", out var topPolicyValue) || topPolicyValue is null)
            {
                return false;
            }

            if (!TryExtractFirstObjectEntry(topPolicyValue, out var topPolicy))
            {
                return false;
            }

            if (!TryReadStringProperty(topPolicy, "VehicleRegistrationNumber", out registration))
            {
                registration = "N/A";
            }

            return TryReadDecimalProperty(topPolicy, "IDV", out idv);
        }

        private static bool TryExtractTopClaimVehicle(
            IReadOnlyDictionary<string, object> computed,
            out int policyId,
            out int claimCount)
        {
            policyId = 0;
            claimCount = 0;

            if (!computed.TryGetValue("topClaimVehicle", out var topClaimVehicleValue) || topClaimVehicleValue is null)
            {
                return false;
            }

            if (!TryExtractFirstObjectEntry(topClaimVehicleValue, out var topClaimVehicle))
            {
                return false;
            }

            var hasPolicyId = TryReadIntProperty(topClaimVehicle, "PolicyId", out policyId);
            var hasClaimCount = TryReadIntProperty(topClaimVehicle, "Count", out claimCount);

            return hasPolicyId && hasClaimCount;
        }

        private static bool TryExtractFirstObjectEntry(object value, out JsonElement element)
        {
            var serialized = JsonSerializer.SerializeToElement(value, MinimalContextJsonOptions);

            if (serialized.ValueKind == JsonValueKind.Object)
            {
                element = serialized;
                return true;
            }

            if (serialized.ValueKind == JsonValueKind.Array && serialized.GetArrayLength() > 0)
            {
                var first = serialized[0];
                if (first.ValueKind == JsonValueKind.Object)
                {
                    element = first;
                    return true;
                }
            }

            element = default;
            return false;
        }

        private static bool TryReadStringProperty(JsonElement source, string propertyName, out string value)
        {
            value = string.Empty;
            if (!TryGetPropertyIgnoreCase(source, propertyName, out var property))
            {
                return false;
            }

            value = property.ValueKind switch
            {
                JsonValueKind.String => property.GetString() ?? string.Empty,
                JsonValueKind.Number => property.ToString(),
                _ => string.Empty
            };

            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryReadDecimalProperty(JsonElement source, string propertyName, out decimal value)
        {
            value = 0m;
            if (!TryGetPropertyIgnoreCase(source, propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var decimalValue))
            {
                value = decimalValue;
                return true;
            }

            if (property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), out var parsed))
            {
                value = parsed;
                return true;
            }

            return false;
        }

        private static bool TryReadIntProperty(JsonElement source, string propertyName, out int value)
        {
            value = 0;
            if (!TryGetPropertyIgnoreCase(source, propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var intValue))
            {
                value = intValue;
                return true;
            }

            if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsed))
            {
                value = parsed;
                return true;
            }

            return false;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement source, string propertyName, out JsonElement value)
        {
            foreach (var property in source.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static string BuildFilterPrefix(IReadOnlyDictionary<string, object> computed)
        {
            var vehicleType = computed.TryGetValue("appliedVehicleType", out var vehicleValue)
                ? vehicleValue?.ToString()
                : null;
            var fuelType = computed.TryGetValue("appliedFuelType", out var fuelValue)
                ? fuelValue?.ToString()
                : null;

            if (string.IsNullOrWhiteSpace(vehicleType) && string.IsNullOrWhiteSpace(fuelType))
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(vehicleType) && !string.IsNullOrWhiteSpace(fuelType))
            {
                return $"Among {vehicleType} {fuelType} vehicles, ";
            }

            if (!string.IsNullOrWhiteSpace(vehicleType))
            {
                return $"Among {vehicleType} vehicles, ";
            }

            return $"Among {fuelType} vehicles, ";
        }

        private static ChatResponseDto MergeResponses(LlmResponseDto llmResponse, ChatResponseDto deterministicFallback, ContextDataDto context)
        {
            var llmAnswer = llmResponse.Answer?.Trim() ?? string.Empty;
            var useDeterministic = string.IsNullOrWhiteSpace(llmAnswer)
                || llmAnswer.Contains("insufficient data", StringComparison.OrdinalIgnoreCase);

            if (useDeterministic)
            {
                deterministicFallback.RulesApplied = context.RulesUsed;
                if (deterministicFallback.DataUsed.Count == 0)
                {
                    deterministicFallback.DataUsed = context.DataUsed;
                }
                return deterministicFallback;
            }

            return new ChatResponseDto
            {
                Answer = llmResponse.Answer,
                Reasoning = llmResponse.Reasoning,
                RulesApplied = llmResponse.RulesApplied.Count > 0 ? llmResponse.RulesApplied : context.RulesUsed,
                DataUsed = llmResponse.DataUsed.Count > 0 ? llmResponse.DataUsed : context.DataUsed,
                Confidence = llmResponse.Confidence
            };
        }

        private ContextDataDto BuildMinimalContextData(
            QueryPlan plan,
            QueryExecutionResult result,
            IReadOnlyList<PolicyContextDto> policiesForContext,
            IReadOnlyDictionary<string, object> computed,
            BusinessRuleAnalysisDto precomputedAnalysis,
            IReadOnlyList<string> ragRules)
        {
            _ = plan;

            var compactClaims = result.Claims
                .Take(5)
                .Select(c => new
                {
                    c.ClaimId,
                    c.PolicyId,
                    c.Status,
                    c.RejectionReason
                })
                .ToList();

            var compactPolicies = policiesForContext
                .Take(5)
                .Select(p => new
                {
                    p.PolicyId,
                    p.IDV,
                    p.PremiumAmount,
                    p.VehicleType,
                    p.FuelType,
                    p.VehicleRegistrationNumber
                })
                .ToList();

            var compactRejectedSummary = result.RejectedSummary
                .Select(s => new { s.Reason, s.Count })
                .ToList();

            var violationCodes = precomputedAnalysis.Violations
                .Select(v => v.Code)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var coreComputed = BuildCoreComputedForLlm(computed);
            var topResult = BuildTopResultForLlm(computed);

            var minimalContext = new
            {
                computed = coreComputed,
                topResult,
                summary = new
                {
                    totalClaims = result.TotalClaims,
                    totalPolicies = policiesForContext.Count,
                    eligibility = precomputedAnalysis.Eligibility,
                    violations = violationCodes.Take(3).ToList(),
                    rejectedSummary = compactRejectedSummary.Take(3).ToList(),
                    policies = compactPolicies,
                    claims = compactClaims
                }
            };

            var dbJson = JsonSerializer.Serialize(minimalContext, MinimalContextJsonOptions);

            if (dbJson.Length > PreferredContextCharLimit)
            {
                var reducedContext = new
                {
                    computed = coreComputed,
                    topResult,
                    summary = new
                    {
                        totalClaims = result.TotalClaims,
                        totalPolicies = policiesForContext.Count,
                        eligibility = precomputedAnalysis.Eligibility,
                        violations = violationCodes.Take(3).ToList(),
                        rejectedSummary = compactRejectedSummary.Take(2).ToList(),
                        policies = compactPolicies.Take(3).ToList(),
                        claims = compactClaims.Take(3).ToList()
                    }
                };

                dbJson = JsonSerializer.Serialize(reducedContext, MinimalContextJsonOptions);
            }

            if (dbJson.Length > PreferredContextCharLimit)
            {
                var ultraCompactContext = new
                {
                    computed = coreComputed,
                    topResult,
                    summary = new
                    {
                        totalClaims = result.TotalClaims,
                        totalPolicies = policiesForContext.Count,
                        eligibility = precomputedAnalysis.Eligibility,
                        violations = violationCodes.Take(2).ToList(),
                        rejectedSummary = compactRejectedSummary.Take(1).ToList()
                    }
                };

                dbJson = JsonSerializer.Serialize(ultraCompactContext, MinimalContextJsonOptions);
            }

            if (dbJson.Length > PreferredContextCharLimit)
            {
                var minimalCountsOnly = new
                {
                    computed = coreComputed,
                    topResult,
                    summary = new
                    {
                        totalClaims = result.TotalClaims,
                        totalPolicies = policiesForContext.Count,
                        eligibility = precomputedAnalysis.Eligibility
                    }
                };

                dbJson = JsonSerializer.Serialize(minimalCountsOnly, MinimalContextJsonOptions);
            }

            if (dbJson.Length > PreferredContextCharLimit)
            {
                var hardMinimal = new
                {
                    computed = coreComputed,
                    topResult = new { },
                    summary = new
                    {
                        totalClaims = result.TotalClaims,
                        totalPolicies = policiesForContext.Count
                    }
                };

                dbJson = JsonSerializer.Serialize(hardMinimal, MinimalContextJsonOptions);
            }

            var rulesUsed = ragRules
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var dataUsed = new List<string>();
            if (result.Claims.Count > 0) dataUsed.Add("claims");
            if (policiesForContext.Count > 0) dataUsed.Add("policies");
            if (computed.Count > 0) dataUsed.Add("computed");
            dataUsed.Add("topResult");
            dataUsed.Add("summary");
            if (violationCodes.Count > 0) dataUsed.Add("violations");

            return new ContextDataDto
            {
                DbJson = dbJson,
                RulesText = rulesUsed.Count > 0 ? string.Join("\n\n", rulesUsed) : "No matching business rules found.",
                DataUsed = dataUsed,
                RulesUsed = rulesUsed
            };
        }

        private static IReadOnlyDictionary<string, object> BuildCoreComputedForLlm(IReadOnlyDictionary<string, object> computed)
        {
            var core = new Dictionary<string, object>();
            if (computed.TryGetValue("highestIDV", out var highestIdv)) core["highestIDV"] = highestIdv;
            if (computed.TryGetValue("totalPremium", out var totalPremium)) core["totalPremium"] = totalPremium;
            if (computed.TryGetValue("pendingCount", out var pendingCount)) core["pendingCount"] = pendingCount;
            return core;
        }

        private static object BuildTopResultForLlm(IReadOnlyDictionary<string, object> computed)
        {
            computed.TryGetValue("topPolicy", out var topPolicy);
            computed.TryGetValue("topClaimVehicle", out var topClaimVehicle);

            return new
            {
                topPolicy,
                topClaimVehicle
            };
        }

        private void PersistSessionMemory(string sessionId, IntentResultDto intent, QueryPlan plan, QueryExecutionResult result)
        {
            var memory = intent.ContextMemory ?? new ContextMemoryDto();
            memory.LastPlanType = plan.PlanType.ToString();
            memory.LastRangeDays = plan.RangeDays > 0 ? plan.RangeDays : memory.LastRangeDays;

            if (IsExplicitNewIntentQuery(plan.NormalizedQuestion))
            {
                memory.AnchorPolicyId = null;
                memory.AnchorVehicleId = null;
            }

            var anchorPolicy = result.Policies.FirstOrDefault();
            if (anchorPolicy != null && IsSingleEntityPlan(plan.PlanType))
            {
                memory.AnchorPolicyId = anchorPolicy.PolicyId;
                memory.AnchorVehicleId = anchorPolicy.VehicleId;
                memory.EntityType = "policy";
                memory.LastEntityId = anchorPolicy.PolicyId.ToString();
            }

            _sessionMemoryService.Upsert(sessionId, memory);
        }

        private static string NormalizeSessionId(string? sessionId)
        {
            return string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId.Trim();
        }

        private static int NormalizeRange(int? requestedRange)
        {
            if (!requestedRange.HasValue || requestedRange.Value <= 0)
            {
                return DefaultExpiryRangeDays;
            }

            return Math.Clamp(requestedRange.Value, 1, 365);
        }

        private static List<string> BuildDataUsed(QueryExecutionResult result)
        {
            var dataUsed = new List<string>();
            if (result.Claims.Count > 0) dataUsed.Add("claims");
            if (result.Users.Count > 0) dataUsed.Add("users");
            if (result.Policies.Count > 0) dataUsed.Add("policies");
            if (result.Vehicles.Count > 0) dataUsed.Add("vehicles");
            if (result.Payments.Count > 0) dataUsed.Add("payments");
            if (result.PaymentAggregates != null) dataUsed.Add("paymentAggregates");
            if (result.Applications.Count > 0) dataUsed.Add("applications");
            if (result.Garages.Count > 0) dataUsed.Add("garages");
            if (result.Notifications.Count > 0) dataUsed.Add("notifications");
            if (result.Referrals.Count > 0) dataUsed.Add("referrals");
            if (result.ReferralAbuseSignals.Count > 0) dataUsed.Add("referralAbuseSignals");
            return dataUsed;
        }

        private static bool ContainsAny(string input, params string[] values)
        {
            return values.Any(input.Contains);
        }

        private QueryType ClassifyQueryType(string question)
        {
            var q = (question ?? string.Empty).ToLowerInvariant();

            if (q.Contains("how many") || q.Contains("count") || q.Contains("summary") || q.Contains("top"))
            {
                return QueryType.Analytics;
            }

            if (q.Contains("why") || q.Contains("reason") || q.Contains("eligible") || q.Contains("violation"))
            {
                return QueryType.Rule;
            }

            if (q.Contains("what is") || q.Contains("define") || q.Contains("meaning"))
            {
                return QueryType.Knowledge;
            }

            return QueryType.Lookup;
        }

        private static bool ShouldUseMemoryForQuery(string lowerQuestion)
        {
            if (IsExplicitNewIntentQuery(lowerQuestion))
            {
                return false;
            }

            if (FollowUpPronounRegex.IsMatch(lowerQuestion))
            {
                return true;
            }

            return ContainsAny(lowerQuestion, "give me registration number", "registration no", "registration details");
        }

        private static string FormatVehicleTypeLabel(string? rawVehicleType)
        {
            if (string.IsNullOrWhiteSpace(rawVehicleType))
            {
                return "Unknown";
            }

            var normalized = NormalizeVehicleType(rawVehicleType);
            return normalized switch
            {
                "TWOWHEELER" => "TwoWheeler",
                "FOURWHEELER" => "FourWheeler",
                "HEAVYVEHICLE" => "HeavyVehicle",
                _ => rawVehicleType.Trim()
            };
        }

        private static bool IsExplicitNewIntentQuery(string lowerQuestion)
        {
            return ContainsAny(
                lowerQuestion,
                "which vehicles",
                "what all vehicles",
                "list",
                "all policies",
                "show all",
                "expiring",
                "expiry",
                "total premium",
                "premium collected",
                "pending premium",
                "pending payment",
                "highest idv",
                "zero depreciation",
                "referral",
                "referrer",
                "referee",
                "suspicious",
                "abuse",
                "application",
                "notifications",
                "ev",
                "car",
                "two-wheeler",
                "bike",
                "plan");
        }

        private static bool ShouldForceDeterministicResponse(QueryPlan plan, QueryType queryType, bool isClaimAnalyticsQuery, bool hasRankingIntent, FilterCriteria filters)
        {
            if (isClaimAnalyticsQuery || hasRankingIntent || HasAppliedFilter(filters))
            {
                return true;
            }

            if (queryType == QueryType.Analytics || queryType == QueryType.Lookup)
            {
                return true;
            }

            return plan.PlanType is
                QueryPlanType.ExpiringPolicies or
                QueryPlanType.ReferralReview or
                QueryPlanType.PendingPaymentPolicies or
                QueryPlanType.TotalPremium or
                QueryPlanType.TotalPremiumCollected or
                QueryPlanType.TotalPremiumPending;
        }

        private static bool CanReturnEmptyResult(QueryPlanType planType)
        {
            return planType is
                QueryPlanType.RejectedClaimsToday or
                QueryPlanType.RejectedClaims or
                QueryPlanType.ExpiringPolicies or
                QueryPlanType.PendingPaymentPolicies or
                QueryPlanType.HighestIdvPolicy or
                QueryPlanType.ZeroDepreciationPolicies or
                QueryPlanType.ReferralReview or
                QueryPlanType.UserPolicies or
                QueryPlanType.UserClaims or
                QueryPlanType.UserVehicles or
                QueryPlanType.VehicleTypesByClaims or
                QueryPlanType.RecentUsers or
                QueryPlanType.RecentVehicles or
                QueryPlanType.RecentApplications or
                QueryPlanType.RecentNotifications;
        }

        private static int NormalizeRiskLevel(string? riskLevel)
        {
            return (riskLevel ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "CRITICAL" => 4,
                "HIGH" => 3,
                "MEDIUM" => 2,
                "LOW" => 1,
                _ => 0
            };
        }

        private static bool IsSingleEntityPlan(QueryPlanType planType)
        {
            return planType is QueryPlanType.HighestIdvPolicy
                or QueryPlanType.PolicyById
                or QueryPlanType.VehicleRegistrationFromPolicy
                or QueryPlanType.ClaimById
                or QueryPlanType.ClaimByNumber
                or QueryPlanType.VehicleWithMostClaims
                or QueryPlanType.HighestClaimPayout;
        }

        private static int? ExtractClaimId(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return null;
            }

            var match = Regex.Match(question, @"\bclaim\s*id\s*[:#-]?\s*(\d+)\b", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return int.TryParse(match.Groups[1].Value, out var claimId) ? claimId : null;
        }

        private static string ExtractClaimNumber(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return string.Empty;
            }

            var normalized = question.Trim();

            var explicitClaimNumber = Regex.Match(
                normalized,
                @"\b(CL[MN][\s\-]?[0-9]{4}[\s\-]?[A-Z0-9]{4,8})\b",
                RegexOptions.IgnoreCase);
            if (explicitClaimNumber.Success)
            {
                return explicitClaimNumber.Groups[1].Value.Replace(" ", string.Empty).ToUpperInvariant();
            }

            var taggedMatch = Regex.Match(
                normalized,
                @"\bclaim\s*(?:number|no|#)\s*[:\-]?\s*([A-Z0-9\-]{6,25})\b",
                RegexOptions.IgnoreCase);
            if (taggedMatch.Success)
            {
                return taggedMatch.Groups[1].Value.Replace(" ", string.Empty).ToUpperInvariant();
            }

            return string.Empty;
        }

        private static IReadOnlyList<T> ApplyCap<T>(IReadOnlyList<T> source)
        {
            if (source.Count <= MaxRelevantRecords)
            {
                return source;
            }

            return source.Take(MaxRelevantRecords).ToList();
        }

        private static bool IsPendingPaymentStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var normalized = status.Trim().Replace(" ", string.Empty).ToLowerInvariant();
            return normalized == "pendingpayment" || normalized == "1";
        }

        private static ChatResponseDto BuildInsufficient(string reason)
        {
            return new ChatResponseDto
            {
                Answer = "Insufficient data to answer.",
                Reasoning = reason,
                Confidence = "LOW",
                RulesApplied = new List<string>(),
                DataUsed = new List<string>()
            };
        }

        public enum QueryType
        {
            Lookup,
            Rule,
            Analytics,
            Knowledge
        }

        public class FilterCriteria
        {
            public string? VehicleType { get; set; }
            public string? FuelType { get; set; }
        }

        private enum QueryPlanType
        {
            Unmapped,
            RejectedClaimsToday,
            RejectedClaims,
            GaragesList,
            UserWithMostClaims,
            VehicleWithMostClaims,
            VehicleTypesByClaims,
            HighestClaimPayout,
            ExpiringPolicies,
            PendingPaymentPolicies,
            HighestIdvPolicy,
            TotalPremium,
            TotalPremiumCollected,
            TotalPremiumPending,
            PolicyPremiumById,
            ZeroDepreciationPolicies,
            VehicleRegistrationFromPolicy,
            PolicyById,
            ClaimById,
            ClaimByNumber,
            UserPolicies,
            UserClaims,
            UserVehicles,
            RecentUsers,
            RecentVehicles,
            RecentApplications,
            RecentNotifications,
            ReferralReview
        }

        private sealed class QueryPlan
        {
            public QueryPlanType PlanType { get; init; }
            public int? PolicyId { get; init; }
            public int? ClaimId { get; init; }
            public string ClaimNumber { get; init; } = string.Empty;
            public int? UserId { get; init; }
            public int RangeDays { get; init; } = DefaultExpiryRangeDays;
            public string VehicleTypeFilter { get; init; } = string.Empty;
            public string FuelTypeFilter { get; init; } = string.Empty;
            public string PlanTypeFilter { get; init; } = string.Empty;
            public bool IncludeClaims { get; init; }
            public bool IncludeUsers { get; init; }
            public bool IncludePolicies { get; init; }
            public bool IncludeVehicles { get; init; }
            public bool IncludePayments { get; init; }
            public bool IncludeApplications { get; init; }
            public bool IncludeGarages { get; init; }
            public bool IncludeNotifications { get; init; }
            public bool IncludeReferrals { get; init; }
            public bool RequireRag { get; init; }
            public string NormalizedQuestion { get; init; } = string.Empty;
            public string RequiredDataHint { get; init; } = string.Empty;
        }

        private sealed class QueryExecutionResult
        {
            public List<ClaimContextDto> Claims { get; } = new();
            public List<UserContextDto> Users { get; } = new();
            public List<PolicyContextDto> Policies { get; } = new();
            public List<VehicleContextDto> Vehicles { get; } = new();
            public List<PaymentContextDto> Payments { get; } = new();
            public List<RejectedSummaryItem> RejectedSummary { get; set; } = new();
            public PaymentAggregateContextDto? PaymentAggregates { get; set; }
            public List<VehicleApplicationContextDto> Applications { get; } = new();
            public List<GarageContextDto> Garages { get; } = new();
            public List<NotificationContextDto> Notifications { get; } = new();
            public List<ReferralContextDto> Referrals { get; } = new();
            public List<ReferralAbuseSignalDto> ReferralAbuseSignals { get; } = new();
            public decimal? TotalPremiumOverride { get; set; }
            public int TotalClaims { get; set; }
            public int TotalPolicies { get; set; }
            public int? TopClaimCount { get; set; }
            public int? TopUserId { get; set; }
            public int? TopPolicyId { get; set; }
        }

        private sealed class RejectedSummaryItem
        {
            public string Reason { get; init; } = string.Empty;
            public int Count { get; init; }
        }
    }
}
