using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// Exposes the D-065 limited, read-only directory of users whose memberships belong to
/// currently valid companies in a corporate group. It does not grant access to member-company
/// accounting data.
/// </summary>
public static class CorporateGroupDirectoryEndpoints
{
    public static IEndpointRouteBuilder MapCorporateGroupDirectoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/corporate-groups");

        group.MapGet("/{groupId:guid}/directory", GetDirectoryAsync)
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        return endpoints;
    }

    public static async Task<IResult> GetDirectoryAsync(
        Guid groupId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromServices] Data.DatabaseContext databaseContext,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        int page = 1,
        int pageSize = 25,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var normalizedSearch = search?.Trim();
        if (normalizedSearch?.Length > 100)
        {
            return TypedResults.Problem(
                title: "عبارت جستجو بیش از حد مجاز است.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";

        // Resolve only a fully provisioned, single-customer group. A malformed legacy group
        // that crosses customer boundaries is hidden rather than partially exposed.
        var corporateGroup = await databaseContext.CorporateGroups
            .AsNoTracking()
            .Where(item => item.Id == groupId && item.IsActive)
            .Where(item => item.OwnerCompany.IsActive && item.OwnerCompany.CustomerBoundaryId != null)
            .Where(item => !item.Members.Any(member =>
                member.Company.CustomerBoundaryId == null ||
                member.Company.CustomerBoundaryId != item.OwnerCompany.CustomerBoundaryId))
            .Select(item => new
            {
                item.Id,
                item.OwnerCompanyId,
                CustomerBoundaryId = item.OwnerCompany.CustomerBoundaryId,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (corporateGroup is null ||
            !await companyAccess.HasPermissionAsync(
                userId.Value,
                corporateGroup.OwnerCompanyId,
                Infrastructure.CorporateGroups.CorporateGroupPermissions.View,
                cancellationToken))
        {
            // Do not reveal whether an inaccessible group exists.
            return TypedResults.NotFound();
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var query =
            from groupMember in databaseContext.CorporateGroupMembers.AsNoTracking()
            join membership in databaseContext.UserCompanyMemberships.AsNoTracking()
                on groupMember.CompanyId equals membership.TenantId
            join account in databaseContext.Users.AsNoTracking()
                on membership.UserId equals account.Id
            join company in databaseContext.Companies.AsNoTracking()
                on membership.TenantId equals company.Id
            join role in databaseContext.CompanyRoles.AsNoTracking()
                on new { Id = membership.RoleId, TenantId = membership.TenantId }
                equals new { Id = role.Id, role.TenantId }
            where groupMember.CorporateGroupId == corporateGroup.Id
                && groupMember.ValidFrom <= today
                && (groupMember.ValidTo == null || groupMember.ValidTo >= today)
                && groupMember.EndedAtUtc == null
                && company.IsActive
                && company.CustomerBoundaryId == corporateGroup.CustomerBoundaryId
            select new
            {
                UserId = account.Id,
                account.Username,
                account.FirstName,
                account.LastName,
                CompanyId = company.Id,
                CompanyName = company.Name,
                IsMembershipActive = membership.IsActive && account.IsActive && company.IsActive && role.IsActive,
            };

        if (!string.IsNullOrEmpty(normalizedSearch))
        {
            query = query.Where(item =>
                (item.Username != null && item.Username.Contains(normalizedSearch)) ||
                (item.FirstName != null && item.FirstName.Contains(normalizedSearch)) ||
                (item.LastName != null && item.LastName.Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var safePage = Math.Clamp(page, 1, 100_000);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (safePage - 1) * safePageSize;

        var rows = await query
            .OrderBy(item => item.CompanyName)
            .ThenBy(item => item.LastName)
            .ThenBy(item => item.FirstName)
            .ThenBy(item => item.Username)
            .ThenBy(item => item.UserId)
            .Skip(skip)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(item =>
        {
            var displayName = $"{item.FirstName} {item.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = item.Username ?? item.UserId.ToString("N");
            }

            return new CorporateGroupDirectoryItemResponse(
                item.UserId,
                displayName,
                item.Username,
                item.CompanyId,
                item.CompanyName,
                item.IsMembershipActive);
        }).ToArray();

        return TypedResults.Ok(new CorporateGroupDirectoryResponse(
            items,
            safePage,
            safePageSize,
            totalCount));
    }
}

public sealed record CorporateGroupDirectoryItemResponse(
    Guid UserId,
    string DisplayName,
    string? Username,
    Guid CompanyId,
    string CompanyName,
    bool IsMembershipActive);

public sealed record CorporateGroupDirectoryResponse(
    IReadOnlyList<CorporateGroupDirectoryItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
