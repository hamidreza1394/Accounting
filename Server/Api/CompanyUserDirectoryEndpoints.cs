using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// A company-scoped directory of users who have a membership row in the requested company.
/// It is intentionally separate from the installation-wide administrator user list.
/// </summary>
public static class CompanyUserDirectoryEndpoints
{
    private const string ListUsersPermission = "users.list";
    private const int MaxPage = 100_000;
    private const int MaxPageSize = 100;
    private const int MaxSearchLength = 120;

    public static RouteGroupBuilder MapCompanyUserDirectoryEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/companies/{companyId:guid}/users")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        users.MapGet("", GetUsersAsync);
        return users;
    }

    public static async Task<IResult> GetUsersAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        Infrastructure.Tenancy.CompanyAccessService companyAccessService,
        string? search = null,
        bool? membershipActive = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        SetNoStore(httpContext);

        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        var companyAccess = await companyAccessService.FindAccessibleCompanyAsync(
            userId.Value, companyId, cancellationToken);
        if (companyAccess is null)
            return TypedResults.NotFound();

        if (!await companyAccessService.HasPermissionAsync(
                userId.Value, companyId, ListUsersPermission, cancellationToken))
        {
            return TypedResults.Forbid();
        }

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > MaxSearchLength)
            return TypedResults.BadRequest(new { error = "عبارت جستجو بیش از حد مجاز است." });

        page = Math.Clamp(page, 1, MaxPage);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query =
            from membership in database.UserCompanyMemberships.AsNoTracking()
            join user in database.Users.AsNoTracking()
                on membership.UserId equals user.Id
            join role in database.CompanyRoles.AsNoTracking()
                on new { membership.RoleId, membership.TenantId }
                equals new { RoleId = role.Id, role.TenantId }
            where membership.TenantId == companyId
            select new CompanyUserDirectoryRow(
                user.Id,
                user.Username,
                user.FirstName,
                user.LastName,
                user.IsActive,
                membership.IsActive,
                role.Id,
                role.Code,
                role.Name,
                role.IsActive,
                membership.CreatedAtUtc);

        if (membershipActive.HasValue)
            query = query.Where(item => item.MembershipIsActive == membershipActive.Value);

        if (search is not null)
        {
            query = query.Where(item =>
                (item.Username != null && item.Username.Contains(search)) ||
                (item.FirstName != null && item.FirstName.Contains(search)) ||
                (item.LastName != null && item.LastName.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(item => item.LastName ?? string.Empty)
            .ThenBy(item => item.FirstName ?? string.Empty)
            .ThenBy(item => item.Username ?? string.Empty)
            .ThenBy(item => item.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(item => new CompanyUserDirectoryItem(
            item.UserId,
            item.Username,
            item.FirstName,
            item.LastName,
            item.UserIsActive,
            item.MembershipIsActive,
            item.RoleId,
            item.RoleCode,
            item.RoleName,
            item.RoleIsActive,
            item.MembershipCreatedAtUtc)).ToArray();

        return TypedResults.Ok(new CompanyUserDirectoryResponse(
            companyId, companyAccess.CompanyName, items, totalCount, page, pageSize));
    }

    private static void SetNoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }

    private sealed record CompanyUserDirectoryRow(
        Guid UserId,
        string? Username,
        string? FirstName,
        string? LastName,
        bool UserIsActive,
        bool MembershipIsActive,
        Guid RoleId,
        string RoleCode,
        string RoleName,
        bool RoleIsActive,
        DateTime MembershipCreatedAtUtc);
}

public sealed record CompanyUserDirectoryResponse(
    Guid CompanyId,
    string CompanyName,
    IReadOnlyList<CompanyUserDirectoryItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Minimal company-directory contract; never exposes contact, credential, session or audit payload fields.
/// </summary>
public sealed record CompanyUserDirectoryItem(
    Guid UserId,
    string? Username,
    string? FirstName,
    string? LastName,
    bool UserIsActive,
    bool MembershipIsActive,
    Guid RoleId,
    string RoleCode,
    string RoleName,
    bool RoleIsActive,
    DateTime MembershipCreatedAtUtc);
