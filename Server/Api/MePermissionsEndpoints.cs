using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// Returns only the permissions for the company selected in the current protected work context.
/// This endpoint is a UI hint; authorization is enforced independently by each operation.
/// </summary>
public static class MePermissionsEndpoints
{
    public static IEndpointRouteBuilder MapMePermissionsEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/me/permissions", GetPermissionsAsync)
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        return api;
    }

    public static async Task<IResult> GetPermissionsAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromServices] Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] Infrastructure.Tenancy.WorkContextService workContextService,
        CancellationToken cancellationToken = default)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";

        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        // Re-read the account and its installation role on every request. Neither an old role
        // claim nor a client-supplied company identifier is an authorization source.
        var account = await database.Users
            .AsNoTracking()
            .Where(user => user.Id == userId.Value && user.IsActive)
            .Select(user => new
            {
                IsInstallationAdministrator = user.Role != null &&
                    user.Role.IsActive &&
                    user.Role.Name == Constants.Role.Admin,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null)
            return TypedResults.Unauthorized();

        var sessionId = Infrastructure.Tenancy.WorkContextCookie.GetSessionId(principal);
        var current = sessionId is null
            ? null
            : await workContextService.GetCurrentAsync(
                httpContext, userId.Value, sessionId.Value, cancellationToken);

        if (current is null)
        {
            return TypedResults.Ok(new MePermissionsResponse(
                account.IsInstallationAdministrator,
                null));
        }

        var permissionCodes = await companyAccess.GetGrantedPermissionCodesAsync(
            userId.Value, current.Company.CompanyId, cancellationToken);

        return TypedResults.Ok(new MePermissionsResponse(
            account.IsInstallationAdministrator,
            new MeCompanyPermissionsResponse(current.Company.CompanyId, permissionCodes)));
    }
}

public sealed record MePermissionsResponse(
    bool IsInstallationAdministrator,
    MeCompanyPermissionsResponse? Company);

public sealed record MeCompanyPermissionsResponse(
    Guid Id,
    IReadOnlyList<string> PermissionCodes);
