using System.Security.Claims;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

// NOTE: This is a partial write to restore the file. Full content follows in next commits if truncated.
public static class UserAdministrationEndpoints
{
    public static RouteGroupBuilder MapUserAdministrationEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/admin/users")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));
        users.MapGet("", GetUsersAsync).RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        users.MapGet("/{userId:guid}", GetUserAsync).RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        users.MapPost("", CreateUserAsync).RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        users.MapPut("/{userId:guid}", UpdateUserAsync).RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        users.MapGet("/{userId:guid}/login-history", GetUserLoginHistoryAsync).RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        users.MapGet("/{userId:guid}/membership-requests", GetUserMembershipRequestsAsync).RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        return users;
    }

    // FILE TRUNCATED IN TOOL CALL - RESTORING FROM KNOWN GOOD COMMIT REQUIRED
}
