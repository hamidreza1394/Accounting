using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// D-078: creates a user account for — or adds an existing account to — the route company.
/// The same-company membership becomes active immediately; no Pending request, no
/// destination re-approval, and no role, permission or fiscal grant of any kind (the role
/// stays unset until <c>roles.assign</c>, under the D-071 delegation ceiling).
/// Cross-company provisioning is impossible by construction: the actor must already hold
/// an active membership plus <c>users.create</c> in the very company of the route, so a
/// manager of company A can never create an active membership in company B; the only path
/// into another company remains the destination-approved membership request workflow.
/// </summary>
public static class CompanyUserProvisioningEndpoints
{
    /// <summary>The company-scoped permission gating this endpoint (D-078 seed).</summary>
    public const string CreateUserPermissionCode = "users.create";

    public static RouteGroupBuilder MapCompanyUserProvisioningEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/companies/{companyId:guid}/users")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        users.MapPost("", CreateUserForCompanyAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        return users;
    }

    /// <summary>
    /// POST /api/v1/companies/{companyId}/users (sensitive rate limit + anti-forgery).
    /// The response is deliberately uniform: 201 whether the account was created or an
    /// existing account was reused, so the endpoint cannot be abused to enumerate which
    /// usernames exist in the installation. The payload states what actually happened.
    /// </summary>
    public static async Task<IResult> CreateUserForCompanyAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] CreateUserRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        Infrastructure.Security.PasswordHashService passwordHashService,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actorId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (actorId is null)
            return TypedResults.Unauthorized();

        var (authorizationFailure, isInstallationAdministrator) = await AuthorizeCompanyUserCreatorAsync(
            database, companyAccess, actorId.Value, companyId, cancellationToken);
        if (authorizationFailure is not null)
            return authorizationFailure;

        if (request is null)
            return TypedResults.BadRequest(new { error = "اطلاعات کاربر یا سیاست رمز عبور معتبر نیست." });
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);

        if (UserCreationValidation.Validate(request,
                out var email, out var username, out var firstName, out var lastName,
                out var cellPhone, out var password) is { } validationError)
            return TypedResults.BadRequest(new { error = validationError });

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // The username is the identity anchor: an existing account is reused as-is. The
        // password and profile fields of the request are ignored for an existing account —
        // a company manager must never be able to overwrite or reset someone else's
        // credentials through this flow (D-078, scenario B).
        var existingUser = await database.Users
            .FirstOrDefaultAsync(user => user.Username != null && user.Username.ToLower() == username.ToLower(),
                cancellationToken);
        if (existingUser is not null)
            return await AddExistingUserToCompanyAsync(
                httpContext, database, existingUser, companyId, actorId.Value,
                isInstallationAdministrator, now, cancellationToken);

        return await CreateNewUserWithMembershipAsync(
            httpContext, database, passwordHashService, companyId, actorId.Value,
            isInstallationAdministrator, request, email, username, firstName, lastName,
            cellPhone, password, now, cancellationToken);
    }

    private static async Task<IResult> AddExistingUserToCompanyAsync(
        HttpContext httpContext,
        Data.DatabaseContext database,
        Domain.User existingUser,
        Guid companyId,
        Guid actorId,
        bool isInstallationAdministrator,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!existingUser.IsActive)
            return TypedResults.Conflict(new
            {
                error = "حساب کاربری با این نام کاربری فعال نیست؛ فعال‌سازی حساب از مسیر مدیر نصب سامانه است.",
            });

        var existingMembership = await database.UserCompanyMemberships
            .FirstOrDefaultAsync(membership =>
                membership.UserId == existingUser.Id &&
                membership.TenantId == companyId,
                cancellationToken);
        if (existingMembership is { IsActive: true })
            return TypedResults.Conflict(new { error = "کاربر در این شرکت از قبل عضویت فعال دارد." });
        // An inactive membership keeps its history; reactivating it is a decision of the
        // company approval workflow (the same contract the roles.assign path enforces).
        if (existingMembership is not null)
            return TypedResults.Conflict(new
            {
                error = "عضویت جدید یا غیرفعال باید ابتدا از مسیر درخواست و تأیید شرکت تعیین تکلیف شود.",
            });

        // No role is set here (D-078): the member has no operational permission until a
        // role is assigned through the separate roles.assign flow under the D-071 ceiling.
        database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = existingUser.Id,
            TenantId = companyId,
            RoleId = null,
            Role = null,
            IsActive = true,
            CreatedAtUtc = now,
        });
        database.SecurityAuditEvents.Add(CreateAudit(
            actorId, companyId, "UserMembershipCreated", existingUser.Id, now, httpContext,
            new
            {
                userId = existingUser.Id,
                userCreated = false,
                membershipCreated = true,
                roleId = (string?)null,
            }));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // The (UserId, TenantId) unique index is the final guard against a concurrent
            // identical provisioning of the same member.
            return TypedResults.Conflict(new { error = "عضویت کاربر در این شرکت هم‌زمان ایجاد شده است." });
        }

        SetNoStore(httpContext);
        return TypedResults.Created(
            $"/api/v1/companies/{companyId}/users",
            new CompanyUserProvisionedResponse(existingUser.Id, UserCreated: false));
    }

    private static async Task<IResult> CreateNewUserWithMembershipAsync(
        HttpContext httpContext,
        Data.DatabaseContext database,
        Infrastructure.Security.PasswordHashService passwordHashService,
        Guid companyId,
        Guid actorId,
        bool isInstallationAdministrator,
        CreateUserRequest request,
        string email,
        string username,
        string? firstName,
        string? lastName,
        string? cellPhone,
        string password,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var duplicate = await database.Users.AsNoTracking().AnyAsync(user =>
            user.EmailAddress.ToLower() == email.ToLower() ||
            user.Username != null && user.Username.ToLower() == username.ToLower() ||
            cellPhone != null && user.CellPhoneNumber == cellPhone,
            cancellationToken);
        if (duplicate)
            return TypedResults.Conflict(new { error = "نام کاربری، ایمیل یا شماره همراه قبلاً ثبت شده است." });

        var user = new Domain.User(email)
        {
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            CellPhoneNumber = cellPhone,
            Password = passwordHashService.Hash(password),
            IsActive = request.IsActive,
            IsSystemic = false,
            IsUndeletable = false,
        };

        // User and membership are saved in one SaveChanges (one transaction): if the
        // membership insert fails, no incomplete user remains (D-078, atomicity).
        database.Users.Add(user);
        database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = user.Id,
            TenantId = companyId,
            RoleId = null,
            Role = null,
            IsActive = true,
            CreatedAtUtc = now,
        });
        database.SecurityAuditEvents.Add(CreateAudit(
            actorId, companyId, "UserCreated", user.Id, now, httpContext,
            new { username = user.Username, isActive = user.IsActive }));
        database.SecurityAuditEvents.Add(CreateAudit(
            actorId, companyId, "UserMembershipCreated", user.Id, now, httpContext,
            new
            {
                userId = user.Id,
                userCreated = true,
                membershipCreated = true,
                roleId = (string?)null,
            }));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // Either a concurrent identical user creation or (theoretically) a concurrent
            // membership row; the unique indexes decide, the client gets one stable answer.
            return TypedResults.Conflict(new { error = "نام کاربری، ایمیل یا شماره همراه قبلاً ثبت شده است." });
        }

        SetNoStore(httpContext);
        return TypedResults.Created(
            $"/api/v1/companies/{companyId}/users",
            new CompanyUserProvisionedResponse(user.Id, UserCreated: true));
    }

    /// <summary>
    /// Dual-mode authorization (D-071 pattern): the installation administrator re-read from
    /// the database, or an active member of the route company holding the company-scoped
    /// <c>users.create</c> permission. A caller without any relationship to the company —
    /// including a manager of another company — receives the same 404 as an unknown
    /// company, so cross-company probing reveals nothing (D-013/D-064).
    /// </summary>
    private static async Task<(IResult? Failure, bool IsInstallationAdministrator)>
        AuthorizeCompanyUserCreatorAsync(
            Data.DatabaseContext database,
            Infrastructure.Tenancy.CompanyAccessService companyAccess,
            Guid actorId,
            Guid companyId,
            CancellationToken cancellationToken)
    {
        if (await UserAdministrationEndpoints.IsInstallationAdministratorAsync(
                actorId, database, cancellationToken))
        {
            if (!companyId.Equals(Guid.Empty) && await database.Companies.AsNoTracking()
                    .AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken))
                return (null, true);
            return (TypedResults.NotFound(), false);
        }

        if (await companyAccess.FindAccessibleCompanyAsync(actorId, companyId, cancellationToken) is null)
            return (TypedResults.NotFound(), false);

        if (!await companyAccess.HasPermissionAsync(
                actorId, companyId, CreateUserPermissionCode, cancellationToken))
            return (TypedResults.Json(
                new { error = "شما مجاز به ایجاد کاربر در این شرکت نیستید." },
                statusCode: StatusCodes.Status403Forbidden), false);

        return (null, false);
    }

    private static Domain.SecurityAuditEvent CreateAudit(
        Guid actorId,
        Guid companyId,
        string eventType,
        Guid targetUserId,
        DateTime occurredAtUtc,
        HttpContext httpContext,
        object details) => new()
    {
        UserId = actorId,
        TenantId = companyId,
        EventType = eventType,
        EntityType = "User",
        EntityId = targetUserId.ToString(),
        OccurredAtUtc = occurredAtUtc,
        CorrelationId = System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier,
        IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
        DetailsJson = System.Text.Json.JsonSerializer.Serialize(details),
    };

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is SqlException sqlException && sqlException.Number is 2601 or 2627)
                return true;
        }
        return false;
    }

    private static void SetNoStore(HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";
    }
}

public sealed record CompanyUserProvisionedResponse(Guid UserId, bool UserCreated);
