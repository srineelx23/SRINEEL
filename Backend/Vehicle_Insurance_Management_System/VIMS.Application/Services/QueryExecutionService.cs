using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;
using VIMS.Domain.Entities;
using VIMS.Domain.Enums;

namespace VIMS.Application.Services
{
    public class QueryExecutionService : IQueryExecutionService
    {
        private const string UnauthorizedMessage = "You are not authorized to access this data.";
        private const string NoRecordsMessage = "No records found.";
        private const string InvalidEntityMessage = "I couldn't understand your request.";

        public Task<string?> ExecuteAsync(
            string query,
            AgentDecision decision,
            int userId,
            string role,
            List<Policy> policies,
            List<Claims> claims,
            List<VehicleApplication> applications,
            List<Payment> payments,
            List<PolicyPlan> plans)
        {
            _ = payments;

            if (decision == null || string.IsNullOrWhiteSpace(decision.Entity))
            {
                return Task.FromResult<string?>(InvalidEntityMessage);
            }

            if (!TryParseRole(role, out var parsedRole))
            {
                return Task.FromResult<string?>(UnauthorizedMessage);
            }

            var ctx = new ExecutionContextData(
                policies ?? [],
                claims ?? [],
                applications ?? [],
                plans ?? []);

            var enhanced = EnhanceQuery(query, decision);

            if (!ValidateAccess(query, enhanced, parsedRole))
            {
                return Task.FromResult<string?>(UnauthorizedMessage);
            }

            var response = RouteToEntityHandler(enhanced, parsedRole, userId, ctx);
            return Task.FromResult<string?>(response);
        }

        private static bool ValidateAccess(string query, EnhancedDecision decision, UserRole role)
        {
            if (role == UserRole.Customer)
            {
                if (ContainsAny(query, "officer", "claims officer"))
                {
                    return false;
                }

                if (ContainsAny(query, "other customer", "other customers", "all customers", "everyone", "all claims", "all policies", "all applications"))
                {
                    return false;
                }

                if (decision.Entity == "OFFICER" || decision.Entity == "CLAIMSOFFICER")
                {
                    return false;
                }
            }

            if (role == UserRole.ClaimsOfficer && decision.Entity != "CLAIMS" && decision.Entity != "PLANS")
            {
                return false;
            }

            return true;
        }

        private static EnhancedDecision EnhanceQuery(string query, AgentDecision decision)
        {
            return QueryEnhancer.Enhance(query, decision);
        }

        private static string RouteToEntityHandler(EnhancedDecision decision, UserRole role, int userId, ExecutionContextData ctx)
        {
            return decision.Entity switch
            {
                "CLAIMS" => ExecuteClaimsQuery(decision, role, userId, ctx.Claims, ctx.Policies),
                "POLICIES" => ExecutePoliciesQuery(decision, role, userId, ctx.Policies),
                "APPLICATIONS" => ExecuteApplicationsQuery(decision, role, userId, ctx.Applications),
                "PLANS" => ExecutePlansQuery(decision, ctx.Plans),
                _ => InvalidEntityMessage
            };
        }

        private static string ExecuteClaimsQuery(
            EnhancedDecision decision,
            UserRole role,
            int userId,
            List<Claims> claims,
            List<Policy> policies)
        {
            var policyIdsByAgent = policies
                .Where(p => p.AgentId == userId)
                .Select(p => p.PolicyId)
                .ToHashSet();

            var secured = claims.Where(c => role switch
            {
                UserRole.Customer => c.CustomerId == userId,
                UserRole.Agent => policyIdsByAgent.Contains(c.PolicyId),
                UserRole.ClaimsOfficer => c.ClaimsOfficerId == userId,
                UserRole.Admin => true,
                _ => false
            });

            if (!string.IsNullOrWhiteSpace(decision.StatusFilter))
            {
                secured = secured.Where(c => c.Status.ToString().Equals(decision.StatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(decision.ClaimTypeFilter))
            {
                secured = secured.Where(c => c.claimType.ToString().Equals(decision.ClaimTypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            var filtered = secured.ToList();
            var aggregate = ExecuteAggregation(decision.Aggregation, filtered.Select(c => c.ApprovedAmount));
            if (aggregate != null)
            {
                return aggregate.WithLabel("claims");
            }

            var sorted = ApplyClaimSorting(filtered, decision.SortBy);
            var limited = ApplyLimit(sorted, decision.Limit);

            if (limited.Count == 0)
            {
                return NoRecordsMessage;
            }

            return ResponseFormatter.FormatClaims(limited);
        }

        private static string ExecutePoliciesQuery(
            EnhancedDecision decision,
            UserRole role,
            int userId,
            List<Policy> policies)
        {
            var secured = policies.Where(p => role switch
            {
                UserRole.Customer => p.CustomerId == userId,
                UserRole.Agent => p.AgentId == userId,
                UserRole.Admin => true,
                _ => false
            });

            if (!string.IsNullOrWhiteSpace(decision.StatusFilter))
            {
                secured = secured.Where(p => p.Status.ToString().Equals(decision.StatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            var filtered = secured.ToList();
            var aggregate = ExecuteAggregation(decision.Aggregation, filtered.Select(p => (decimal?)p.PremiumAmount));
            if (aggregate != null)
            {
                return aggregate.WithLabel("policies");
            }

            var sorted = ApplyPolicySorting(filtered, decision.SortBy);
            var limited = ApplyLimit(sorted, decision.Limit);

            if (limited.Count == 0)
            {
                return NoRecordsMessage;
            }

            if (decision.RegistrationOnly)
            {
                return ResponseFormatter.FormatRegistrationNumbers(limited);
            }

            if (decision.VehicleIntent)
            {
                return ResponseFormatter.FormatVehicles(limited);
            }

            return ResponseFormatter.FormatPolicies(limited);
        }

        private static string ExecuteApplicationsQuery(
            EnhancedDecision decision,
            UserRole role,
            int userId,
            List<VehicleApplication> applications)
        {
            var secured = applications.Where(a => role switch
            {
                UserRole.Customer => a.CustomerId == userId,
                UserRole.Agent => a.AssignedAgentId == userId,
                UserRole.Admin => true,
                _ => false
            });

            if (!string.IsNullOrWhiteSpace(decision.StatusFilter))
            {
                secured = secured.Where(a => a.Status.ToString().Equals(decision.StatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            var filtered = secured.ToList();
            var aggregate = ExecuteAggregation(decision.Aggregation, filtered.Select(a => (decimal?)a.InvoiceAmount));
            if (aggregate != null)
            {
                return aggregate.WithLabel("applications");
            }

            var sorted = ApplyApplicationSorting(filtered, decision.SortBy);
            var limited = ApplyLimit(sorted, decision.Limit);

            if (limited.Count == 0)
            {
                return NoRecordsMessage;
            }

            if (decision.RegistrationOnly)
            {
                return string.Join("\n", limited.Select(a => a.RegistrationNumber));
            }

            return ResponseFormatter.FormatApplications(limited);
        }

        private static string ExecutePlansQuery(EnhancedDecision decision, List<PolicyPlan> plans)
        {
            var query = plans.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(decision.StatusFilter))
            {
                query = query.Where(p => p.Status.ToString().Equals(decision.StatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            var filtered = query.ToList();
            var aggregate = ExecuteAggregation(decision.Aggregation, filtered.Select(p => (decimal?)p.BasePremium));
            if (aggregate != null)
            {
                return aggregate.WithLabel("plans");
            }

            var sorted = ApplyPlanSorting(filtered, decision.SortBy);
            var limited = ApplyLimit(sorted, decision.Limit);

            if (limited.Count == 0)
            {
                return NoRecordsMessage;
            }

            return ResponseFormatter.FormatPlans(limited);
        }

        private static AggregationResult? ExecuteAggregation(string aggregation, IEnumerable<decimal?> values)
        {
            if (string.IsNullOrWhiteSpace(aggregation) || aggregation.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var nonNull = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            var normalized = aggregation.Trim().ToUpperInvariant();

            if (normalized == "COUNT")
            {
                return new AggregationResult("Total", nonNull.Count.ToString());
            }

            if (nonNull.Count == 0)
            {
                return new AggregationResult("Total", "0");
            }

            return normalized switch
            {
                "SUM" => new AggregationResult("Total", ResponseFormatter.FormatCurrency(nonNull.Sum())),
                "AVG" => new AggregationResult("Average", ResponseFormatter.FormatCurrency(nonNull.Average())),
                _ => null
            };
        }

        private static List<Claims> ApplyClaimSorting(List<Claims> source, string sortBy)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                return source.OrderByDescending(c => c.CreatedAt).ToList();
            }

            if (sortBy.Contains("CreatedAt asc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderBy(c => c.CreatedAt).ToList();
            }

            if (sortBy.Contains("CreatedAt desc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderByDescending(c => c.CreatedAt).ToList();
            }

            if (sortBy.Contains("ApprovedAmount asc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderBy(c => c.ApprovedAmount).ToList();
            }

            if (sortBy.Contains("ApprovedAmount desc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderByDescending(c => c.ApprovedAmount).ToList();
            }

            return source;
        }

        private static List<Policy> ApplyPolicySorting(List<Policy> source, string sortBy)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                return source.OrderByDescending(p => p.StartDate).ToList();
            }

            if (sortBy.Contains("StartDate asc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderBy(p => p.StartDate).ToList();
            }

            if (sortBy.Contains("StartDate desc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderByDescending(p => p.StartDate).ToList();
            }

            if (sortBy.Contains("PremiumAmount asc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderBy(p => p.PremiumAmount).ToList();
            }

            if (sortBy.Contains("PremiumAmount desc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderByDescending(p => p.PremiumAmount).ToList();
            }

            return source;
        }

        private static List<VehicleApplication> ApplyApplicationSorting(List<VehicleApplication> source, string sortBy)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                return source.OrderByDescending(a => a.CreatedAt).ToList();
            }

            if (sortBy.Contains("CreatedAt asc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderBy(a => a.CreatedAt).ToList();
            }

            if (sortBy.Contains("CreatedAt desc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderByDescending(a => a.CreatedAt).ToList();
            }

            if (sortBy.Contains("InvoiceAmount asc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderBy(a => a.InvoiceAmount).ToList();
            }

            if (sortBy.Contains("InvoiceAmount desc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderByDescending(a => a.InvoiceAmount).ToList();
            }

            return source;
        }

        private static List<PolicyPlan> ApplyPlanSorting(List<PolicyPlan> source, string sortBy)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                return source.OrderBy(p => p.BasePremium).ToList();
            }

            if (sortBy.Contains("BasePremium asc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderBy(p => p.BasePremium).ToList();
            }

            if (sortBy.Contains("BasePremium desc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderByDescending(p => p.BasePremium).ToList();
            }

            if (sortBy.Contains("MaxCoverageAmount asc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderBy(p => p.MaxCoverageAmount).ToList();
            }

            if (sortBy.Contains("MaxCoverageAmount desc", StringComparison.OrdinalIgnoreCase))
            {
                return source.OrderByDescending(p => p.MaxCoverageAmount).ToList();
            }

            return source;
        }

        private static List<T> ApplyLimit<T>(List<T> source, int limit)
        {
            return limit > 0 ? source.Take(limit).ToList() : source;
        }

        private static bool TryParseRole(string role, out UserRole parsedRole)
        {
            if (Enum.TryParse<UserRole>(role, true, out parsedRole))
            {
                return true;
            }

            if (string.Equals(role, "OFFICER", StringComparison.OrdinalIgnoreCase))
            {
                parsedRole = UserRole.ClaimsOfficer;
                return true;
            }

            return false;
        }

        private static bool ContainsAny(string query, params string[] tokens)
        {
            return tokens.Any(token => query.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        private sealed record ExecutionContextData(
            List<Policy> Policies,
            List<Claims> Claims,
            List<VehicleApplication> Applications,
            List<PolicyPlan> Plans);

        private sealed record AggregationResult(string Prefix, string Value)
        {
            public string WithLabel(string entityLabel)
            {
                return Prefix switch
                {
                    "Average" => $"Average {Singularize(entityLabel)} amount: {Value}",
                    _ => $"Total {entityLabel}: {Value}"
                };
            }

            private static string Singularize(string entity)
            {
                return entity.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                    ? entity[..^1]
                    : entity;
            }
        }

        private sealed class EnhancedDecision
        {
            public string Entity { get; init; } = string.Empty;
            public string Aggregation { get; init; } = "NONE";
            public string SortBy { get; init; } = string.Empty;
            public int Limit { get; init; }
            public string StatusFilter { get; init; } = string.Empty;
            public string ClaimTypeFilter { get; init; } = string.Empty;
            public bool VehicleIntent { get; init; }
            public bool RegistrationOnly { get; init; }
        }

        private static class QueryEnhancer
        {
            public static EnhancedDecision Enhance(string query, AgentDecision decision)
            {
                var q = (query ?? string.Empty).Trim();
                var filters = CloneFilters(decision.Filters);

                PopulateMissingStatusFilter(q, filters, decision.Entity);
                PopulateMissingClaimTypeFilter(q, filters);

                var sortBy = (decision.SortBy ?? string.Empty).Trim();
                var limit = decision.Limit < 0 ? 0 : decision.Limit;

                ApplyFirstLatestDefaults(q, decision.Entity, ref sortBy, ref limit);

                return new EnhancedDecision
                {
                    Entity = (decision.Entity ?? string.Empty).Trim().ToUpperInvariant(),
                    Aggregation = string.IsNullOrWhiteSpace(decision.Aggregation)
                        ? "NONE"
                        : decision.Aggregation.Trim().ToUpperInvariant(),
                    SortBy = sortBy,
                    Limit = limit,
                    StatusFilter = GetFilter(filters, "status"),
                    ClaimTypeFilter = GetFilter(filters, "claimType"),
                    VehicleIntent = ContainsAny(q, "vehicle", "vehicles"),
                    RegistrationOnly = ContainsAny(q, "registration number", "reg number", "registration")
                };
            }

            private static Dictionary<string, string> CloneFilters(Dictionary<string, string>? filters)
            {
                return filters == null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(filters, StringComparer.OrdinalIgnoreCase);
            }

            private static void PopulateMissingStatusFilter(string query, Dictionary<string, string> filters, string? entity)
            {
                if (!string.IsNullOrWhiteSpace(GetFilter(filters, "status")))
                {
                    return;
                }

                if (ContainsAny(query, "rejected"))
                {
                    filters["status"] = "Rejected";
                    return;
                }

                if (ContainsAny(query, "approved"))
                {
                    filters["status"] = "Approved";
                    return;
                }

                if (ContainsAny(query, "active") && string.Equals(entity, "POLICIES", StringComparison.OrdinalIgnoreCase))
                {
                    filters["status"] = "Active";
                    return;
                }

                if (ContainsAny(query, "pending"))
                {
                    filters["status"] = "Submitted";
                }
            }

            private static void PopulateMissingClaimTypeFilter(string query, Dictionary<string, string> filters)
            {
                if (!string.IsNullOrWhiteSpace(GetFilter(filters, "claimType")))
                {
                    return;
                }

                if (ContainsAny(query, "theft"))
                {
                    filters["claimType"] = "Theft";
                    return;
                }

                if (ContainsAny(query, "damage"))
                {
                    filters["claimType"] = "Damage";
                }
            }

            private static void ApplyFirstLatestDefaults(string query, string? entity, ref string sortBy, ref int limit)
            {
                if (!string.IsNullOrWhiteSpace(sortBy) && limit > 0)
                {
                    return;
                }

                var normalizedEntity = (entity ?? string.Empty).Trim().ToUpperInvariant();

                if (ContainsAny(query, "first", "earliest") && normalizedEntity == "POLICIES")
                {
                    sortBy = "StartDate asc";
                    limit = 1;
                    return;
                }

                if (ContainsAny(query, "latest") && normalizedEntity == "CLAIMS")
                {
                    sortBy = "CreatedAt desc";
                    limit = 1;
                    return;
                }

                if (ContainsAny(query, "first", "earliest") && normalizedEntity == "CLAIMS")
                {
                    sortBy = "CreatedAt asc";
                    limit = 1;
                }
            }

            private static string GetFilter(Dictionary<string, string> filters, string key)
            {
                if (filters.TryGetValue(key, out var value))
                {
                    return (value ?? string.Empty).Trim();
                }

                return string.Empty;
            }
        }

        private static class ResponseFormatter
        {
            private const string Rupee = "\u20B9";

            public static string FormatClaims(List<Claims> claims)
            {
                return string.Join("\n", claims.Select(c =>
                    $"{c.ClaimNumber} | {c.Status} | {c.CreatedAt:yyyy-MM-dd}"));
            }

            public static string FormatPolicies(List<Policy> policies)
            {
                return string.Join("\n", policies.Select(p =>
                    $"{p.PolicyNumber} | {p.Status} | {FormatCurrency(p.PremiumAmount)}"));
            }

            public static string FormatVehicles(List<Policy> policies)
            {
                var vehicles = policies
                    .Where(p => p.Vehicle != null)
                    .Select(p => $"{p.Vehicle.RegistrationNumber} | {p.Vehicle.Make} {p.Vehicle.Model} | {p.Vehicle.FuelType}")
                    .ToList();

                return vehicles.Count == 0 ? NoRecordsMessage : string.Join("\n", vehicles);
            }

            public static string FormatRegistrationNumbers(List<Policy> policies)
            {
                var values = policies
                    .Where(p => p.Vehicle != null)
                    .Select(p => p.Vehicle.RegistrationNumber)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return values.Count == 0 ? NoRecordsMessage : string.Join("\n", values);
            }

            public static string FormatApplications(List<VehicleApplication> applications)
            {
                return string.Join("\n", applications.Select(a =>
                    $"{a.VehicleApplicationId} | {a.Status} | {a.RegistrationNumber} | {a.CreatedAt:yyyy-MM-dd}"));
            }

            public static string FormatPlans(List<PolicyPlan> plans)
            {
                return string.Join("\n", plans.Select(p =>
                    $"{p.PlanName} | Base Premium: {FormatCurrency(p.BasePremium)} | Max Coverage: {FormatCurrency(p.MaxCoverageAmount)}"));
            }

            public static string FormatCurrency(decimal value)
            {
                return $"{Rupee}{value:N0}";
            }
        }
    }
}
