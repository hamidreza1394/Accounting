using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// Read-only administration endpoints for installation administrators.
/// All authorization decisions are revalidated against the current database state.
/// </summary>
public static class UserAdministrationEndpoints
{
    public static RouteGroupBuilder MapUserAdministrationEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/admin/users")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        users.MapGet("", GetUsersAsync);
        users.MapGet("/{userId:guid}", GetUserAsync);
        return users;
    }

    public static async Task<IResult> GetUsersAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        string? search,
        string? status,
        int page,
        int pageSize,
        string? sort,
        bool descending,
        CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;

        if (!TryNormalizeStatus(status, out var normalizedStatus) ||
            !TryNormalizeSort(sort, out var normalizedSort))
            return TypedResults.BadRequest(new { error = "فیلتر یا مرتب‌سازی معتبر نیست." });

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > 120)
            return TypedResults.BadRequest(new { error = "عبارت جستجو بیش از حد طولانی است." });

        IQueryable<Domain.User> query = database.Users.AsNoTracking();

        if (normalizedStatus == "active")
            query = query.Where(user => user.IsActive);
        else if (normalizedStatus == "inactive")
            query = query.Where(user => !user.IsActive);

        if (search is not null)
        {
            query = query.Where(user =>
                (user.Username != null && user.Username.Contains(search)) ||
                (user.FirstName != null && user.FirstName.Contains(search)) ||
                (user.LastName != null && user.LastName.Contains(search)) ||
                user.EmailAddress.Contains(search) ||
                (user.CellPhoneNumber != null && user.CellPhoneNumber.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplySort(query, normalizedSort, descending);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new UserListItemResponse(
                user.Id,
                user.Username,
                user.FirstName,
                user.LastName,
                user.EmailAddress,
                user.CellPhoneNumber,
                user.IsActive,
                user.LastLoginDateTime,
                user.InsertDateTime,
                database.UserCompanyMemberships.Count(membership =>
                    membership.UserId == user.Id && membership.IsActive)))
            .ToListAsync(cancellationToken);

        SetNoStore(httpContext);
        return TypedResults.Ok(new UserListResponse(items, totalCount, page, pageSize));
    }

    public static async Task<IResult> GetUserAsync(
        Guid userId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;

        if (userId == Guid.Empty)
            return TypedResults.NotFound();

        var user = await database.Users
            .AsNoTracking()
            .Where(current => current.Id == userId)
            .Select(current => new
            {
                current.Id,
                current.Username,
                current.FirstName,
                current.LastName,
                current.EmailAddress,
                current.CellPhoneNumber,
                current.IsEmailAddressVerified,
                current.IsCellPhoneNumberVerified,
                current.IsActive,
                current.LastLoginDateTime,
                current.InsertDateTime,
                current.UpdateDateTime,
                RoleName = current.Role == null ? null : current.Role.Name,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return TypedResults.NotFound();

        var memberships = await database.UserCompanyMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.Company.Name)
            .Select(membership => new UserCompanyMembershipResponse(
                membership.TenantId,
                membership.Company.Name,
                membership.Role.Code,
                membership.Role.Name,
                membership.IsActive,
                membership.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        SetNoStore(httpContext);
        return TypedResults.Ok(new UserDetailsResponse(
            user.Id,
            user.Username,
            user.FirstName,
            user.LastName,
            user.EmailAddress,
            user.CellPhoneNumber,
            user.IsEmailAddressVerified,
            user.IsCellPhoneNumberVerified,
            user.IsActive,
            user.LastLoginDateTime,
            user.InsertDateTime,
            user.UpdateDateTime,
            user.RoleName,
            memberships));
    }

    private static async Task<IResult?> AuthorizeInstallationAdministratorAsync(
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        CancellationToken cancellationToken)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        var isAdministrator = await database.Users
            .AsNoTracking()
            .AnyAsync(user =>
                user.Id == userId.Value &&
                user.IsActive &&
                user.Role != null &&
                user.Role.IsActive &&
                user.Role.Name == Constants.Role.Admin,
                cancellationToken);

        return isAdministrator ? null : TypedResults.Forbid();
    }

    private static IQueryable<Domain.User> ApplySort(
        IQueryable<Domain.User> query,
        string sort,
        bool descending)
    {
        return (sort, descending) switch
        {
            ("username", false) => query.OrderBy(user => user.Username).ThenBy(user => user.Id),
            ("username", true) => query.OrderByDescending(user => user.Username).ThenBy(user => user.Id),
            ("name", false) => query.OrderBy(user => user.LastName).ThenBy(user => user.FirstName).ThenBy(user => user.Id),
            ("name", true) => query.OrderByDescending(user => user.LastName).ThenByDescending(user => user.FirstName).ThenBy(user => user.Id),
            ("lastLogin", false) => query.OrderBy(user => user.LastLoginDateTime).ThenBy(user => user.Id),
            ("lastLogin", true) => query.OrderByDescending(user => user.LastLoginDateTime).ThenBy(user => user.Id),
            ("created", false) => query.OrderBy(user => user.InsertDateTime).ThenBy(user => user.Id),
            _ => query.OrderByDescending(user => user.InsertDateTime).ThenBy(user => user.Id),
        };
    }

    private static bool TryNormalizeStatus(string? status, out string normalized)
    {
        normalized = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim();
        return normalized is "all" or "active" or "inactive";
    }

    private static bool TryNormalizeSort(string? sort, out string normalized)
    {
        normalized = string.IsNullOrWhiteSpace(sort) ? "created" : sort.Trim();
        return normalized is "created" or "lastLogin" or "name" or "username";
    }

    private static void SetNoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }
}

public sealed record UserListResponse(
    IReadOnlyList<UserListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record UserListItemResponse(
    Guid Id,
    string? Username,
    string? FirstName,
    string? LastName,
    string EmailAddress,
    string? CellPhoneNumber,
    bool IsActive,
    DateTime? LastLoginDateTime,
    DateTime InsertDateTime,
    int ActiveCompanyCount);

public sealed record UserDetailsResponse(
    Guid Id,
    string? Username,
    string? FirstName,
    string? LastName,
    string EmailAddress,
    string? CellPhoneNumber,
    bool IsEmailAddressVerified,
    bool IsCellPhoneNumberVerified,
    bool IsActive,
    DateTime? LastLoginDateTime,
    DateTime InsertDateTime,
    DateTime UpdateDateTime,
    string? LegacyRoleName,
    IReadOnlyList<UserCompanyMembershipResponse> Memberships);

public sealed record UserCompanyMembershipResponse(
    Guid CompanyId,
    string CompanyName,
    string RoleCode,
    string RoleName,
    bool IsActive,
    DateTime CreatedAtUtc);
