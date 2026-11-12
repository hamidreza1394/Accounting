using System.Security.Claims;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
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

        users.MapGet("", GetUsersAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        users.MapGet("/{userId:guid}", GetUserAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        users.MapPost("", CreateUserAsync).RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        users.MapPut("/{userId:guid}", UpdateUserAsync).RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        users.MapGet("/{userId:guid}/login-history", GetUserLoginHistoryAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        users.MapGet("/{userId:guid}/membership-requests", GetUserMembershipRequestsAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
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
        bool? descending,
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
                (user.EmailAddress != null && user.EmailAddress.Contains(search)) ||
                (user.CellPhoneNumber != null && user.CellPhoneNumber.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        // Match the established main-branch contract: newest-created first unless direction is explicit.
        query = ApplySort(query, normalizedSort, descending ?? normalizedSort == "created");

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
                membership.Role == null ? null : membership.Role.Code,
                membership.Role == null ? null : membership.Role.Name,
                membership.IsActive,
                membership.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var utcMemberships = memberships.Select(membership => membership with
        {
            CreatedAtUtc = DateTime.SpecifyKind(membership.CreatedAtUtc, DateTimeKind.Utc),
        }).ToArray();

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
            utcMemberships));
    }

    /// <summary>
    /// GET /api/v1/admin/users/{userId}/login-history — the authentication events of the
    /// targeted user, for installation administrators only. Mirrors the self-service
    /// /me/login-history contract (same event types, same ordering, no-store).
    /// </summary>
    public static async Task<IResult> GetUserLoginHistoryAsync(
        Guid userId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;

        if (userId == Guid.Empty)
            return TypedResults.NotFound();

        var userExists = await database.Users.AsNoTracking()
            .AnyAsync(candidate => candidate.Id == userId, cancellationToken);
        if (!userExists)
            return TypedResults.NotFound();

        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";
        var pageSize = Math.Clamp(take, 1, 100);

        var events = await database.SecurityAuditEvents
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Where(item =>
                item.EventType == "LoginSucceeded" ||
                item.EventType == "LoginFailed" ||
                item.EventType == "LoginDeniedInactive" ||
                item.EventType == "LoginDeniedEnrollmentPending" ||
                item.EventType == "LogoutSucceeded")
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(pageSize)
            .Select(item => new LoginHistoryItemResponse(
                item.EventType,
                item.OccurredAtUtc,
                item.IpAddress))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok<IReadOnlyList<LoginHistoryItemResponse>>(events);
    }

    /// <summary>
    /// GET /api/v1/admin/users/{userId}/membership-requests — the inter-company membership
    /// requests of the target user across all destination companies, for installation
    /// administrators only. Read-only projection of the existing workflow table (D-065): no
    /// new status machine, no mutation; decisions are made in the destination-company queue
    /// endpoints, this read only makes the user's request state visible in the profile.
    /// </summary>
    public static async Task<IResult> GetUserMembershipRequestsAsync(
        Guid userId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        string? status = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var authorization = await AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;

        if (userId == Guid.Empty)
            return TypedResults.NotFound();

        var userExists = await database.Users.AsNoTracking()
            .AnyAsync(candidate => candidate.Id == userId, cancellationToken);
        if (!userExists)
            return TypedResults.NotFound();

        SetNoStore(httpContext);
        if (!Server.Api.CompanyMembershipRequestEndpoints.TryNormalizeStatus(status, out var normalizedStatus))
            return TypedResults.BadRequest(new { error = "وضعیت درخواست معتبر نیست." });

        var safePage = Math.Clamp(page, 1, 100_000);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.CompanyMembershipRequests.AsNoTracking()
            .Where(item => item.UserId == userId);
        if (normalizedStatus is not null)
            query = query.Where(item => item.Status == normalizedStatus);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await (
            from item in query
            join decider in database.Users.AsNoTracking()
                on item.DecidedByUserId equals (Guid?)decider.Id into deciderRows
            from decider in deciderRows.DefaultIfEmpty()
            orderby item.RequestedAtUtc descending, item.Id descending
            select new
            {
                item.Id,
                DestinationCompanyId = item.DestinationTenantId,
                DestinationCompanyName = item.DestinationCompany.Name,
                item.Status,
                item.RequestedAtUtc,
                item.DecidedAtUtc,
                item.DecisionReason,
                DeciderFirstName = decider == null ? null : decider.FirstName,
                DeciderLastName = decider == null ? null : decider.LastName,
                DeciderUsername = decider == null ? null : decider.Username,
                ApprovedRoleName = item.ApprovedRole == null ? null : item.ApprovedRole.Name,
            })
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(item =>
        {
            var deciderDisplayName = $"{item.DeciderFirstName} {item.DeciderLastName}".Trim();
            if (string.IsNullOrWhiteSpace(deciderDisplayName))
                deciderDisplayName = item.DeciderUsername;
            return new AdminUserMembershipRequestItemResponse(
                item.Id,
                item.DestinationCompanyId,
                item.DestinationCompanyName,
                item.Status,
                DateTime.SpecifyKind(item.RequestedAtUtc, DateTimeKind.Utc),
                item.DecidedAtUtc is null ? null : DateTime.SpecifyKind(item.DecidedAtUtc.Value, DateTimeKind.Utc),
                deciderDisplayName,
                item.ApprovedRoleName,
                item.DecisionReason);
        }).ToList();

        return TypedResults.Ok(new AdminUserMembershipRequestListResponse(items, safePage, safePageSize, totalCount));
    }

    public static async Task<IResult> CreateUserAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] CreateUserRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        Infrastructure.Security.PasswordHashService passwordHashService,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);

        if (UserCreationValidation.Validate(request,
                out var email, out var username, out var firstName, out var lastName,
                out var cellPhone, out var password) is { } validationError)
            return TypedResults.BadRequest(new { error = validationError });

        // Optional e-mail contract (D-078): the e-mail is compared only when this request
        // has one; accounts without an e-mail are never duplicates of each other. The same
        // shape as the company-scoped provisioning endpoint, so the two flows cannot drift.
        var duplicate = await database.Users.AsNoTracking().AnyAsync(user =>
            (email != null && user.EmailAddress != null && user.EmailAddress.ToLower() == email.ToLower()) ||
            user.Username != null && user.Username.ToLower() == username.ToLower() ||
            cellPhone != null && user.CellPhoneNumber == cellPhone,
            cancellationToken);
        if (duplicate)
            return TypedResults.Conflict(new { error = "نام کاربری، ایمیل یا شماره همراه قبلاً ثبت شده است." });

        var actorId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal)!.Value;
        var user = new Domain.User(email)
        {
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            CellPhoneNumber = cellPhone,
            Password = passwordHashService.Hash(password!),
            IsActive = request.IsActive,
            IsSystemic = false,
            IsUndeletable = false,
        };

        database.Users.Add(user);
        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = actorId,
            EventType = "UserCreated",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                username = user.Username,
                isActive = user.IsActive,
            }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return TypedResults.Conflict(new { error = "نام کاربری، ایمیل یا شماره همراه قبلاً ثبت شده است." });
        }

        SetNoStore(httpContext);
        return TypedResults.Created($"/api/v1/admin/users/{user.Id}", new UserCreatedResponse(user.Id));
    }

    public static async Task<IResult> UpdateUserAsync(
        Guid userId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] UpdateUserRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null)
            return authorization;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);

        if (userId == Guid.Empty)
            return TypedResults.NotFound();

        var user = await database.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
            return TypedResults.NotFound();

        var email = Dtat.Utility.FixText(request.EmailAddress);
        var username = Dtat.Utility.FixText(request.Username);
        var firstName = Dtat.Utility.FixText(request.FirstName);
        var lastName = Dtat.Utility.FixText(request.LastName);
        var cellPhone = Dtat.Utility.FixText(request.CellPhoneNumber);

        // The e-mail is optional on update as well: null, empty and whitespace clear the
        // stored address (FixText already normalized them to null) and a previously given
        // address must stay format- and length-validated — the same contract as create.
        if ((email is not null &&
                (email.Length > Constants.MaxLength.EmailAddress ||
                 !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))) ||
            string.IsNullOrWhiteSpace(username) ||
            username.Length < Constants.MinLength.Username ||
            username.Length > Constants.MaxLength.Username ||
            // The same username policy the Razor views enforce; the API must not be a bypass.
            !new System.ComponentModel.DataAnnotations.RegularExpressionAttribute(Constants.RegularExpression.Username).IsValid(username) ||
            (firstName?.Length > Constants.MaxLength.FirstName) ||
            (lastName?.Length > Constants.MaxLength.LastName) ||
            (cellPhone is not null && (cellPhone.Length != Constants.FixedLength.CellPhoneNumber || !new System.ComponentModel.DataAnnotations.RegularExpressionAttribute(Constants.RegularExpression.CellPhoneNumber).IsValid(cellPhone))))
            return TypedResults.BadRequest(new { error = "اطلاعات کاربر معتبر نیست." });

        // The e-mail comparison runs only when this update carries an e-mail: clearing it
        // (null) never matches any account, so the previous address can simply be removed.
        var duplicate = await database.Users.AsNoTracking().AnyAsync(candidate =>
            candidate.Id != userId &&
            ((email != null && candidate.EmailAddress != null && candidate.EmailAddress.ToLower() == email.ToLower()) ||
             candidate.Username != null && candidate.Username.ToLower() == username.ToLower() ||
             cellPhone != null && candidate.CellPhoneNumber == cellPhone),
            cancellationToken);
        if (duplicate)
            return TypedResults.Conflict(new { error = "نام کاربری، ایمیل یا شماره همراه قبلاً ثبت شده است." });

        var actorId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal)!.Value;
        if (actorId == userId && !request.IsActive)
            return TypedResults.Conflict(new { error = "مدیر سیستم نمی‌تواند حساب جاری خود را غیرفعال کند." });

        await using var adminGuardTransaction = user.IsActive && !request.IsActive && database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (user.IsActive && !request.IsActive && user.RoleId is Guid targetRoleId)
        {
            var targetIsActiveAdmin = await database.Roles.AsNoTracking().AnyAsync(role =>
                role.Id == targetRoleId && role.IsActive && role.Name == Constants.Role.Admin,
                cancellationToken);
            if (targetIsActiveAdmin)
            {
                var activeAdminCount = await database.Users.CountAsync(candidate =>
                    candidate.IsActive && candidate.Role != null &&
                    candidate.Role.IsActive && candidate.Role.Name == Constants.Role.Admin,
                    cancellationToken);
                if (activeAdminCount <= 1)
                    return TypedResults.Conflict(new { error = "غیرفعال‌کردن آخرین مدیر سیستم مجاز نیست." });
            }
        }

        var previousActive = user.IsActive;
        user.ChangeEmailAddress(email);
        user.Username = username;
        user.FirstName = firstName;
        user.LastName = lastName;
        user.ChangeCellPhoneNumber(cellPhone);
        user.IsActive = request.IsActive;
        user.SetUpdateDateTime();

        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = actorId,
            EventType = "UserUpdated",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                changedFields = new[] { "EmailAddress", "Username", "FirstName", "LastName", "CellPhoneNumber", "IsActive" },
                previousIsActive = previousActive,
                currentIsActive = user.IsActive,
            }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (adminGuardTransaction is not null)
                await adminGuardTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return TypedResults.Conflict(new { error = "تغییرات با تعارض داده یا درخواست هم‌زمان مواجه شد؛ وضعیت را دوباره بررسی کنید." });
        }
        catch (System.Data.Common.DbException)
        {
            return TypedResults.Conflict(new { error = "تغییر هم‌زمان دیگری انجام شده است؛ وضعیت مدیران فعال را دوباره بررسی کنید." });
        }

        SetNoStore(httpContext);
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

        return await IsInstallationAdministratorAsync(userId.Value, database, cancellationToken)
            ? null
            : ForbiddenForNonAdministrator();
    }

    /// <summary>
    /// Re-reads the installation-administrator predicate from the database for one known
    /// user id, so dual-mode endpoints (D-071) can branch between the installation scope
    /// and the company scope without producing a result object first.
    /// </summary>
    internal static async Task<bool> IsInstallationAdministratorAsync(
        Guid userId,
        Data.DatabaseContext database,
        CancellationToken cancellationToken)
    {
        return await database.Users
            .AsNoTracking()
            .AnyAsync(user =>
                user.Id == userId &&
                user.IsActive &&
                user.Role != null &&
                user.Role.IsActive &&
                user.Role.Name == Constants.Role.Admin,
                cancellationToken);
    }

    internal static IResult ForbiddenForNonAdministrator() =>
        // Prefer an explicit 403 payload so callers and unit tests can read StatusCode
        // without depending on authentication-handler Forbid semantics.
        TypedResults.Json(
            new { error = "فقط مدیر نصب سامانه مجاز به این عملیات است." },
            statusCode: StatusCodes.Status403Forbidden);

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
    string? EmailAddress,
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
    string? EmailAddress,
    string? CellPhoneNumber,
    bool IsEmailAddressVerified,
    bool IsCellPhoneNumberVerified,
    bool IsActive,
    DateTime? LastLoginDateTime,
    DateTime InsertDateTime,
    DateTime UpdateDateTime,
    string? LegacyRoleName,
    IReadOnlyList<UserCompanyMembershipResponse> Memberships);

// D-078: role fields are null while the member has no company role yet; the membership
// itself stays a valid, active row.
public sealed record UserCompanyMembershipResponse(
    Guid CompanyId,
    string CompanyName,
    string? RoleCode,
    string? RoleName,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record CreateUserRequest(
    string? Username,
    string? FirstName,
    string? LastName,
    string? EmailAddress,
    string? CellPhoneNumber,
    string? Password,
    string? ConfirmPassword,
    bool IsActive);

public sealed record UpdateUserRequest(
    string? Username,
    string? FirstName,
    string? LastName,
    string? EmailAddress,
    string? CellPhoneNumber,
    bool IsActive);

public sealed record UserCreatedResponse(Guid Id);

/// <summary>
/// One inter-company membership request of the target user (administrative read of the
/// existing D-065 workflow table). The projection is allow-listed: no payload, no IP, no
/// row version — the same fields the destination approvers already see, plus the company.
/// </summary>
public sealed record AdminUserMembershipRequestItemResponse(
    Guid Id,
    Guid DestinationCompanyId,
    string DestinationCompanyName,
    string Status,
    DateTime RequestedAtUtc,
    DateTime? DecidedAtUtc,
    string? DecidedByDisplayName,
    string? ApprovedRoleName,
    string? DecisionReason);

public sealed record AdminUserMembershipRequestListResponse(
    IReadOnlyList<AdminUserMembershipRequestItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
