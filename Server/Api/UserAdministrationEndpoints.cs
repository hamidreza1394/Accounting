using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// Installation-administrator user directory endpoints. Company-manager authorization
/// and membership-approval workflows are intentionally out of scope for this slice.
/// </summary>
public static class UserAdministrationEndpoints
{
    public static RouteGroupBuilder MapUserAdministrationEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/admin/users")
            .RequireAuthorization()
            .WithTags("UserAdministration");

        users.MapGet("", ListUsersAsync);
        users.MapGet("/{userId:guid}", GetUserAsync);
        users.MapPost("/{userId:guid}/memberships", AssignMembershipAsync)
            .RequireAntiforgery()
            .RequireRateLimiting(Infrastructure.RateLimiting.ApiRateLimiting.SensitivePolicyName);
        users.MapDelete("/{userId:guid}/memberships/{companyId:guid}", RevokeMembershipAsync)
            .RequireAntiforgery()
            .RequireRateLimiting(Infrastructure.RateLimiting.ApiRateLimiting.SensitivePolicyName);

        return api;
    }

    public static async Task<IResult> ListUsersAsync(
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        CancellationToken cancellationToken,
        string? search = null,
        string? status = null,
        string? sort = null,
        bool descending = true,
        int page = 1,
        int pageSize = 20)
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

        return TypedResults.Ok(new UserListResponse(items, totalCount, page, pageSize));
    }

    public static async Task<IResult> GetUserAsync(
        Guid userId,
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
            .Where(item => item.Id == userId)
            .Select(item => new UserDetailResponse(
                item.Id,
                item.Username,
                item.FirstName,
                item.LastName,
                item.EmailAddress,
                item.CellPhoneNumber,
                item.IsActive,
                item.LastLoginDateTime,
                item.InsertDateTime,
                item.UpdateDateTime,
                database.UserCompanyMemberships
                    .Where(membership => membership.UserId == item.Id)
                    .OrderBy(membership => membership.Company.Name)
                    .Select(membership => new UserMembershipResponse(
                        membership.TenantId,
                        membership.Company.Name,
                        membership.RoleId,
                        membership.Role.Code,
                        membership.Role.Name,
                        membership.IsActive,
                        membership.CreatedAtUtc))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return user is null ? TypedResults.NotFound() : TypedResults.Ok(user);
    }

    public static async Task<IResult> AssignMembershipAsync(
        Guid userId,
        AssignMembershipRequest request,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        IAntiforgery antiforgery,
        Data.DatabaseContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;

        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return TypedResults.BadRequest(new { error = "توکن ضدجعل نامعتبر است." });
        }

        if (userId == Guid.Empty || request.CompanyId == Guid.Empty || request.RoleId == Guid.Empty)
            return TypedResults.BadRequest(new { error = "شناسه‌های ارسالی معتبر نیست." });

        var user = await database.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
            return TypedResults.NotFound();

        var company = await database.Companies.SingleOrDefaultAsync(item => item.Id == request.CompanyId, cancellationToken);
        if (company is null || !company.IsActive)
            return TypedResults.BadRequest(new { error = "شرکت معتبر و فعال نیست." });

        var role = await database.CompanyRoles.SingleOrDefaultAsync(
            item => item.Id == request.RoleId && item.TenantId == request.CompanyId, cancellationToken);
        if (role is null || !role.IsActive)
            return TypedResults.BadRequest(new { error = "نقش شرکتی معتبر و فعال نیست." });

        var membership = await database.UserCompanyMemberships.SingleOrDefaultAsync(
            item => item.UserId == userId && item.TenantId == request.CompanyId, cancellationToken);

        // Fail-closed: do not create or reactivate membership here. Membership approval is a separate slice.
        if (membership is null || !membership.IsActive)
            return TypedResults.Conflict(new { error = "عضویت فعال برای این کاربر در شرکت یافت نشد." });

        var actorId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal)!.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        membership.RoleId = role.Id;
        membership.Role = role;

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            EventType = "CompanyMembershipRoleAssigned",
            ActorUserId = actorId,
            SubjectUserId = userId,
            TenantId = request.CompanyId,
            Detail = role.Code,
            OccurredAtUtc = now,
        });

        await database.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    public static async Task<IResult> RevokeMembershipAsync(
        Guid userId,
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        IAntiforgery antiforgery,
        Data.DatabaseContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;

        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return TypedResults.BadRequest(new { error = "توکن ضدجعل نامعتبر است." });
        }

        if (userId == Guid.Empty || companyId == Guid.Empty)
            return TypedResults.NotFound();

        var membership = await database.UserCompanyMemberships.SingleOrDefaultAsync(
            item => item.UserId == userId && item.TenantId == companyId, cancellationToken);
        if (membership is null)
            return TypedResults.NotFound();

        if (!membership.IsActive)
            return TypedResults.NoContent();

        var actorId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal)!.Value;
        membership.IsActive = false;
        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            EventType = "CompanyMembershipRevoked",
            ActorUserId = actorId,
            SubjectUserId = userId,
            TenantId = companyId,
            Detail = null,
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        });

        await database.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    internal static async Task<IResult?> AuthorizeInstallationAdministratorAsync(
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

        // Prefer an explicit 403 payload so callers and unit tests can read StatusCode
        // without depending on authentication-handler Forbid semantics.
        return isAdministrator
            ? null
            : TypedResults.Json(
                new { error = "فقط مدیر نصب سامانه مجاز به این عملیات است." },
                statusCode: StatusCodes.Status403Forbidden);
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
    int ActiveMembershipCount);

public sealed record UserDetailResponse(
    Guid Id,
    string? Username,
    string? FirstName,
    string? LastName,
    string EmailAddress,
    string? CellPhoneNumber,
    bool IsActive,
    DateTime? LastLoginDateTime,
    DateTime InsertDateTime,
    DateTime UpdateDateTime,
    IReadOnlyList<UserMembershipResponse> Memberships);

public sealed record UserMembershipResponse(
    Guid CompanyId,
    string CompanyName,
    Guid RoleId,
    string RoleCode,
    string RoleName,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record AssignMembershipRequest(Guid CompanyId, Guid RoleId);
