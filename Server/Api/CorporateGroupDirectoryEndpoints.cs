using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// A read-only, allow-listed directory of the users of the companies that belong to one
/// corporate group (D-065 decision 2, D-071). A company manager may only see the member
/// companies of their own group, only with an explicit permission, and only the fields the
/// product needs: no contact data, no usernames, no roles, no permissions, no login or
/// security data. The directory itself grants no management capability whatsoever.
/// </summary>
public static class CorporateGroupDirectoryEndpoints
{
    /// <summary>Permission to view the limited cross-company directory of one's own group.</summary>
    public const string ViewGroupDirectoryPermission = "corporate-groups.directory.view";

    private const int MaxPage = 100_000;
    private const int MaxPageSize = 100;
    private const int MaxSearchLength = 120;

    public static RouteGroupBuilder MapCorporateGroupDirectoryEndpoints(this RouteGroupBuilder api)
    {
        var groups = api.MapGroup("/corporate-groups")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        groups.MapGet("/{groupId:guid}/directory", GetDirectoryAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        return groups;
    }

    public static async Task<IResult> GetDirectoryAsync(
        Guid groupId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        string? search = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        SetNoStore(httpContext);

        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > MaxSearchLength)
            return TypedResults.BadRequest(new { error = "عبارت جستجو بیش از حد مجاز است." });

        var group = await database.CorporateGroups.AsNoTracking()
            .Where(current => current.Id == groupId && current.IsActive)
            .Select(current => new { current.Id, current.Name, current.OwnerCompanyId })
            .FirstOrDefaultAsync(cancellationToken);
        if (group is null)
            return TypedResults.NotFound();

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // The group scope is proven server-side: every company with an open membership row
        // in this group. A closed membership (ValidTo in the past) leaves the scope.
        var scopeCompanies = await database.CorporateGroupMembers.AsNoTracking()
            .Where(member => member.CorporateGroupId == groupId)
            .Where(member => member.Company.IsActive)
            .Where(member => member.ValidTo == null || member.ValidTo >= today)
            .Select(member => new { CompanyId = member.CompanyId, member.Company.Name })
            .ToListAsync(cancellationToken);
        if (scopeCompanies.Count == 0)
            return TypedResults.NotFound();

        // Fail closed on the customer boundary (D-037): the directory is only shown when the
        // owner company and every member company live in one provisioned boundary. A group
        // with no boundary, mixed boundaries or an unprovisioned owner stays invisible.
        var boundaryCompanyIds = scopeCompanies.Select(company => company.CompanyId)
            .Append(group.OwnerCompanyId)
            .Distinct()
            .ToArray();
        var boundaryIds = await database.Companies.AsNoTracking()
            .Where(company => boundaryCompanyIds.Contains(company.Id))
            .Select(company => company.CustomerBoundaryId)
            .ToListAsync(cancellationToken);
        if (boundaryIds.Count != boundaryCompanyIds.Length ||
            boundaryIds.Any(boundaryId => !boundaryId.HasValue) ||
            boundaryIds.Select(boundaryId => boundaryId!.Value).Distinct().Count() != 1)
            return TypedResults.NotFound();

        var scopeCompanyIds = scopeCompanies.Select(company => company.CompanyId).ToArray();

        // The caller must be an active member of one of the scoped companies; anything else
        // is the same 404 as an unknown group, so group existence cannot be probed.
        var memberships = await companyAccess.GetAccessibleCompaniesAsync(userId.Value, cancellationToken);
        var callerCompanyIds = memberships.Select(access => access.CompanyId).ToHashSet();
        if (!scopeCompanyIds.Any(scopeCompanyId => callerCompanyIds.Contains(scopeCompanyId)))
            return TypedResults.NotFound();

        // And the caller must hold the explicit directory permission in at least one of them.
        var permittedCompanyIds = await companyAccess.GetCompaniesWithPermissionAsync(
            userId.Value, scopeCompanyIds, ViewGroupDirectoryPermission, cancellationToken);
        if (permittedCompanyIds.Count == 0)
            return TypedResults.Json(
                new { error = "شما مجاز به مشاهده دفترچه کاربران گروه نیستید." },
                statusCode: StatusCodes.Status403Forbidden);

        var safePage = Math.Clamp(page, 1, MaxPage);
        var safePageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        // Allow-listed projection only: identity of the member companies and the name of the
        // users with an active membership. No username, e-mail, phone, role, permission,
        // membership metadata or any other-company security field is ever selected here.
        var query =
            from membership in database.UserCompanyMemberships.AsNoTracking()
            join user in database.Users.AsNoTracking()
                on membership.UserId equals user.Id
            join company in database.Companies.AsNoTracking()
                on membership.TenantId equals company.Id
            where scopeCompanyIds.Contains(membership.TenantId) &&
                membership.IsActive &&
                membership.Company.IsActive &&
                membership.Role.IsActive &&
                membership.Role.TenantId == membership.TenantId
            select new
            {
                CompanyId = company.Id,
                CompanyName = company.Name,
                UserId = user.Id,
                user.FirstName,
                user.LastName,
            };

        if (search is not null)
        {
            query = query.Where(item =>
                (item.FirstName != null && item.FirstName.Contains(search)) ||
                (item.LastName != null && item.LastName.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(item => item.CompanyName)
            .ThenBy(item => item.LastName ?? string.Empty)
            .ThenBy(item => item.FirstName ?? string.Empty)
            .ThenBy(item => item.UserId)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(item => new CorporateGroupDirectoryItem(
            item.CompanyId,
            item.CompanyName,
            item.UserId,
            item.FirstName,
            item.LastName)).ToArray();

        return TypedResults.Ok(new CorporateGroupDirectoryResponse(
            group.Id, group.Name, items, totalCount, safePage, safePageSize));
    }

    private static void SetNoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }
}

public sealed record CorporateGroupDirectoryResponse(
    Guid GroupId,
    string GroupName,
    IReadOnlyList<CorporateGroupDirectoryItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Allow-listed group-directory entry: no username, no contact data, no role, no
/// permission, no membership lifecycle metadata and no security data (D-065).
/// </summary>
public sealed record CorporateGroupDirectoryItem(
    Guid CompanyId,
    string CompanyName,
    Guid UserId,
    string? FirstName,
    string? LastName);
