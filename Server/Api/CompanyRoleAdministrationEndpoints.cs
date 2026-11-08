using System.Data;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Server.Api;

/// <summary>
/// Manages company-scoped access groups by reusing CompanyRole and CompanyRolePermissions.
/// Permission codes remain server-owned catalog entries; clients may assign only known codes.
/// </summary>
/// <remarks>
/// <para>
/// Every endpoint has two authorized actor kinds (D-071): the installation administrator
/// (re-read from the database on every request, no delegation ceiling) and the company
/// manager — an active member of the routed company whose role carries the matching
/// <c>roles.*</c> permission code. Company managers act strictly inside their company and
/// inside their delegation ceiling: they may only create, edit or assign roles whose
/// permission set is a subset of the permissions they themselves hold in that company.
/// </para>
/// </remarks>
public static class CompanyRoleAdministrationEndpoints
{
    /// <summary>Permission to view the roles, permissions and members of the routed company.</summary>
    public const string ViewRolesPermission = "roles.view";

    /// <summary>Permission to create company roles inside the delegation ceiling.</summary>
    public const string CreateRolesPermission = "roles.create";

    /// <summary>Permission to edit company roles inside the delegation ceiling.</summary>
    public const string UpdateRolesPermission = "roles.update";

    /// <summary>Permission to assign or transfer the roles of active members.</summary>
    public const string AssignRolesPermission = "roles.assign";

    /// <summary>Upper bound of members moved by one transfer request (one atomic save).</summary>
    public const int MaxTransferBatchSize = 500;

    /// <summary>Largest accepted page number; keeps (page - 1) * pageSize inside int range.</summary>
    private const int MaxMemberPage = 100_000;

    private static readonly Regex RoleCodePattern = new("^[A-Z0-9][A-Z0-9._-]{1,79}$", RegexOptions.CultureInvariant);

    public static RouteGroupBuilder MapCompanyRoleAdministrationEndpoints(this RouteGroupBuilder api)
    {
        var roles = api.MapGroup("/admin/companies/{companyId:guid}/roles")
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        roles.MapGet("", GetRolesAsync);
        roles.MapPost("", CreateRoleAsync).RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        roles.MapPut("/{roleId:guid}", UpdateRoleAsync).RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        roles.MapGet("/{roleId:guid}/members", GetRoleMembersAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        roles.MapPost("/{roleId:guid}/members/transfer", TransferMembersAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        roles.MapPost("/{roleId:guid}/members/{userId:guid}", AssignMembershipAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);

        api.MapGet("/admin/companies/{companyId:guid}/effective-permissions/{userId:guid}", GetEffectivePermissionsAsync)
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));
        api.MapGet("/admin/permission-catalog", GetPermissionCatalogAsync)
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));
        api.MapGet("/admin/permission-templates", GetTemplatesAsync)
            .RequireAuthorization(policy => policy.RequireClaim(
                Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType,
                Infrastructure.Tenancy.CompanyAccessService.UserAccountKind));

        return roles;
    }

    public static async Task<IResult> GetRolesAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        CancellationToken cancellationToken)
    {
        var (failure, _) = await AuthorizeCompanyScopeAsync(
            database, companyAccess, principal, companyId, ViewRolesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return TypedResults.NotFound();

        var roles = await database.CompanyRoles.AsNoTracking()
            .Where(role => role.TenantId == companyId)
            .OrderBy(role => role.Name)
            .Select(role => new
            {
                role.Id,
                role.TenantId,
                role.Code,
                role.Name,
                role.IsSystemRole,
                role.IsActive,
                PermissionCount = role.RolePermissions.Count,
                ActiveMemberCount = database.UserCompanyMemberships.Count(membership =>
                    membership.TenantId == companyId && membership.RoleId == role.Id && membership.IsActive),
            })
            .ToListAsync(cancellationToken);
        var roleIds = roles.Select(role => role.Id).ToArray();
        var permissions = await database.CompanyRolePermissions.AsNoTracking()
            .Where(permission => roleIds.Contains(permission.RoleId))
            .Select(permission => new { permission.RoleId, permission.Permission.Code })
            .ToListAsync(cancellationToken);
        var result = roles.Select(role => new CompanyRoleResponse(
            role.Id, role.TenantId, role.Code, role.Name, role.IsSystemRole, role.IsActive,
            role.PermissionCount, role.ActiveMemberCount,
            permissions.Where(permission => permission.RoleId == role.Id)
                .Select(permission => permission.Code).OrderBy(code => code).ToArray()))
            .ToArray();

        SetNoStore(httpContext);
        return TypedResults.Ok(result);
    }

    /// <summary>
    /// GET /admin/companies/{companyId}/effective-permissions/{userId} — دسترسی‌های مؤثر یک
    /// عضو فعال همان شرکت (اجتماع کدهای مجوز نقش‌های فعال عضویت‌های او، D-064/D-071).
    /// فقط-خواندنی؛ برای کاربر بدون عضویت فعال همان شرکت 404 برمی‌گرداند تا وجود حساب افشا نشود.
    /// </summary>
    public static async Task<IResult> GetEffectivePermissionsAsync(
        Guid companyId,
        Guid userId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        CancellationToken cancellationToken)
    {
        var (failure, _) = await AuthorizeCompanyScopeAsync(
            database, companyAccess, principal, companyId, ViewRolesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return TypedResults.NotFound();

        var roleNames = await database.UserCompanyMemberships.AsNoTracking()
            .Where(membership => membership.TenantId == companyId
                && membership.UserId == userId
                && membership.IsActive)
            .Join(database.CompanyRoles.AsNoTracking(),
                membership => new { membership.RoleId, membership.TenantId },
                role => new { RoleId = role.Id, role.TenantId },
                (membership, role) => new { role.Name, role.IsActive })
            .Where(row => row.IsActive)
            .Select(row => row.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (roleNames.Count == 0) return TypedResults.NotFound();

        var codes = await companyAccess.GetGrantedPermissionCodesAsync(userId, companyId, cancellationToken);

        SetNoStore(httpContext);
        return TypedResults.Ok(new EffectivePermissionsResponse(
            userId,
            companyId,
            roleNames.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            codes.OrderBy(code => code, StringComparer.Ordinal).ToArray()));
    }

    public static async Task<IResult> GetPermissionCatalogAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.DelegationCeilingService delegationCeiling,
        CancellationToken cancellationToken)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        var registeredPermissions = await database.PermissionDefinitions.AsNoTracking()
            .OrderBy(permission => permission.Code)
            .Select(permission => new { permission.Id, permission.Code, permission.Name, permission.Description })
            .ToListAsync(cancellationToken);

        if (!await UserAdministrationEndpoints.IsInstallationAdministratorAsync(userId.Value, database, cancellationToken))
        {
            // A company-scoped actor sees only the codes inside its own delegation ceiling
            // (the union across every active membership), so installation-level codes stay
            // invisible and the catalog can never leak a delegatable capability the actor
            // does not already hold (D-071).
            var ceilingCodes = await delegationCeiling.GetUnionPermissionCodesAsync(userId.Value, cancellationToken);
            if (ceilingCodes.Count == 0)
                return TypedResults.NotFound();
            var ceiling = ceilingCodes.ToHashSet(StringComparer.Ordinal);
            registeredPermissions = registeredPermissions
                .Where(permission => ceiling.Contains(permission.Code))
                .ToList();
        }

        var permissions = registeredPermissions.Select(permission => new PermissionCatalogItemResponse(
            permission.Id,
            permission.Code,
            permission.Name,
            permission.Description,
            permission.Code.Split('.')[0])).ToArray();

        SetNoStore(httpContext);
        return TypedResults.Ok(permissions);
    }

    public static async Task<IResult> GetTemplatesAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.DelegationCeilingService delegationCeiling,
        CancellationToken cancellationToken)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return TypedResults.Unauthorized();

        var codes = await database.PermissionDefinitions.AsNoTracking()
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken);

        if (!await UserAdministrationEndpoints.IsInstallationAdministratorAsync(userId.Value, database, cancellationToken))
        {
            // Templates are cut down to the actor's own ceiling, and a template whose whole
            // permission set is outside the ceiling disappears instead of hinting at codes
            // the actor cannot delegate.
            var ceilingCodes = await delegationCeiling.GetUnionPermissionCodesAsync(userId.Value, cancellationToken);
            if (ceilingCodes.Count == 0)
                return TypedResults.NotFound();
            var ceiling = ceilingCodes.ToHashSet(StringComparer.Ordinal);
            codes = codes.Where(code => ceiling.Contains(code)).ToList();
        }

        var templates = BuildTemplates(codes)
            .Where(template => template.PermissionCodes.Count > 0)
            .ToArray();
        SetNoStore(httpContext);
        return TypedResults.Ok(templates);
    }

    public static async Task<IResult> CreateRoleAsync(
        Guid companyId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] CreateCompanyRoleRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] Infrastructure.Tenancy.DelegationCeilingService delegationCeiling,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var (failure, scope) = await AuthorizeCompanyScopeAsync(
            database, companyAccess, principal, companyId, CreateRolesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return TypedResults.NotFound();

        var code = request.Code?.Trim().ToUpperInvariant();
        var name = Dtat.Utility.FixText(request.Name);
        if (string.IsNullOrWhiteSpace(code) || !RoleCodePattern.IsMatch(code) ||
            string.IsNullOrWhiteSpace(name) || name.Length > 120)
            return TypedResults.BadRequest(new { error = "کد و عنوان گروه دسترسی معتبر نیست." });

        var permissionCodes = await ResolvePermissionCodesAsync(
            database, request.TemplateCode, request.PermissionCodes, cancellationToken);
        if (permissionCodes is null)
            return TypedResults.BadRequest(new { error = "الگوی انتخاب‌شده نامعتبر است یا مجوزی در کاتالوگ وجود ندارد." });

        if (await database.CompanyRoles.AnyAsync(role => role.TenantId == companyId && role.Code == code, cancellationToken))
            return TypedResults.Conflict(new { error = "کد گروه دسترسی در این شرکت تکراری است." });

        var permissionIds = await database.PermissionDefinitions
            .Where(permission => permissionCodes.Contains(permission.Code))
            .Select(permission => permission.Id)
            .ToListAsync(cancellationToken);

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        // The delegation ceiling is evaluated inside the transaction from fresh, tracked rows:
        // a concurrent change of the actor's memberships or the catalog can never be missed (D-071).
        Infrastructure.Tenancy.TrackedDelegationCeiling? ceiling = null;
        if (!scope!.IsInstallationAdministrator)
        {
            ceiling = await delegationCeiling.LoadTrackedCeilingAsync(scope.UserId, companyId, cancellationToken);
            if (ceiling is null) return TypedResults.NotFound();
            if (ceiling.Violates(permissionIds, out _))
                return ForbiddenBeyondCeiling();
        }

        var role = new Domain.CompanyRole
        {
            TenantId = companyId,
            Code = code,
            Name = name,
            IsActive = true,
            IsSystemRole = false,
        };
        database.CompanyRoles.Add(role);
        foreach (var permissionId in permissionIds)
            database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission { Role = role, PermissionId = permissionId });

        var actorId = scope!.UserId;
        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = actorId,
            TenantId = companyId,
            EventType = "CompanyRoleCreated",
            EntityType = "CompanyRole",
            EntityId = role.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { role.Code, permissionCodes }),
        });

        try
        {
            ceiling?.ApplyRowVersionGuards(database);
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "سقف واگذاری شما هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }
        catch (DbUpdateException)
        {
            return TypedResults.Conflict(new { error = "کد گروه دسترسی در این شرکت تکراری است." });
        }

        SetNoStore(httpContext);
        return TypedResults.Created($"/api/v1/admin/companies/{companyId}/roles/{role.Id}", new CompanyRoleCreatedResponse(role.Id));
    }

    public static async Task<IResult> UpdateRoleAsync(
        Guid companyId,
        Guid roleId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] UpdateCompanyRoleRequest request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] Infrastructure.Tenancy.DelegationCeilingService delegationCeiling,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var (failure, scope) = await AuthorizeCompanyScopeAsync(
            database, companyAccess, principal, companyId, UpdateRolesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return TypedResults.NotFound();

        var role = await database.CompanyRoles
            .Include(current => current.RolePermissions)
            .FirstOrDefaultAsync(current => current.Id == roleId && current.TenantId == companyId, cancellationToken);
        if (role is null) return TypedResults.NotFound();
        if (role.IsSystemRole) return TypedResults.Conflict(new { error = "گروه سیستمی قابل ویرایش نیست." });
        if (role.IsActive && !request.IsActive &&
            await database.UserCompanyMemberships.AnyAsync(
                membership => membership.TenantId == companyId && membership.RoleId == roleId && membership.IsActive,
                cancellationToken))
            return TypedResults.Conflict(new { error = "ابتدا اعضای فعال این گروه را به گروه دیگری منتقل کنید." });

        var name = Dtat.Utility.FixText(request.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            return TypedResults.BadRequest(new { error = "عنوان گروه دسترسی معتبر نیست." });

        var permissionCodes = await ResolvePermissionCodesAsync(database, null, request.PermissionCodes, cancellationToken);
        if (permissionCodes is null)
            return TypedResults.BadRequest(new { error = "یک یا چند مجوز در کاتالوگ وجود ندارد." });

        var permissionIds = await database.PermissionDefinitions
            .Where(permission => permissionCodes.Contains(permission.Code))
            .Select(permission => permission.Id)
            .ToListAsync(cancellationToken);

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        // Replacing the permission set (or re-activating a dormant role) is a delegation:
        // for a company manager every resulting permission must stay inside the ceiling that
        // is re-read here, inside the transaction, from tracked rows (D-071).
        if (!scope!.IsInstallationAdministrator)
        {
            var ceiling = await delegationCeiling.LoadTrackedCeilingAsync(scope.UserId, companyId, cancellationToken);
            if (ceiling is null) return TypedResults.NotFound();
            if (ceiling.Violates(permissionIds, out _))
                return ForbiddenBeyondCeiling();
            ceiling.ApplyRowVersionGuards(database);
        }

        database.CompanyRolePermissions.RemoveRange(role.RolePermissions);
        role.RolePermissions.Clear();
        foreach (var permissionId in permissionIds)
            role.RolePermissions.Add(new Domain.CompanyRolePermission { Role = role, PermissionId = permissionId });
        role.Name = name;
        role.IsActive = request.IsActive;
        // Force a role-row update even when only its permission links changed (or the
        // submitted scalar values are unchanged), so the rowversion guards the whole role.
        database.Entry(role).Property(current => current.Name).IsModified = true;

        var actorId = scope!.UserId;
        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = actorId,
            TenantId = companyId,
            EventType = "CompanyRoleUpdated",
            EntityType = "CompanyRole",
            EntityId = role.Id.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { role.Name, role.IsActive, permissionCodes }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "این گروه دسترسی هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    public static async Task<IResult> AssignMembershipAsync(
        Guid companyId,
        Guid roleId,
        Guid userId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] Infrastructure.Tenancy.DelegationCeilingService delegationCeiling,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var (failure, scope) = await AuthorizeCompanyScopeAsync(
            database, companyAccess, principal, companyId, AssignRolesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);

        var role = await database.CompanyRoles
            .Include(current => current.RolePermissions)
            .FirstOrDefaultAsync(
                current => current.Id == roleId && current.TenantId == companyId && current.IsActive,
                cancellationToken);
        var company = await database.Companies.FirstOrDefaultAsync(
            current => current.Id == companyId && current.IsActive, cancellationToken);
        var user = await database.Users.FirstOrDefaultAsync(current => current.Id == userId && current.IsActive, cancellationToken);
        if (role is null || company is null || user is null) return TypedResults.NotFound();

        // System roles (e.g. company-admin) are immutable and must never be granted through
        // this path: assigning one would hand out company-wide administration outside the
        // approval workflow that refuses system roles for every actor kind (D-065, D-071).
        // The refusal comes before the ceiling check, so it cannot be talked around by a
        // manager who happens to hold every catalog permission.
        if (role.IsSystemRole)
            return TypedResults.Conflict(new { error = "تخصیص گروه سیستمی مجاز نیست." });

        var membership = await database.UserCompanyMemberships.FirstOrDefaultAsync(
            current => current.UserId == userId && current.TenantId == companyId,
            cancellationToken);

        // A role assignment may only reassign an existing, active membership. New or
        // inactive memberships must go through the destination-company approval workflow (D-065).
        if (membership is null || !membership.IsActive)
            return TypedResults.Conflict(new
            {
                error = "عضویت جدید یا غیرفعال باید ابتدا از مسیر درخواست و تأیید شرکت مقصد تعیین تکلیف شود."
            });

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        // A company manager may never change their own membership from this path and may
        // only assign a role whose permission set is inside their freshly re-read ceiling;
        // both guards fail closed before anything is written (D-071).
        if (!scope!.IsInstallationAdministrator)
        {
            if (userId == scope.UserId)
                return ForbiddenSelfAssignment();
            var ceiling = await delegationCeiling.LoadTrackedCeilingAsync(scope.UserId, companyId, cancellationToken);
            if (ceiling is null) return TypedResults.NotFound();
            if (ceiling.Violates(role.RolePermissions.Select(rolePermission => rolePermission.PermissionId), out _))
                return ForbiddenBeyondCeiling();
            ceiling.ApplyRowVersionGuards(database);
        }

        var previousRoleId = membership.RoleId;
        membership.RoleId = roleId;
        membership.Role = role;
        // Guard both sides of the transition: a concurrent membership edit or role
        // deactivation must win or lose atomically rather than silently being overwritten.
        database.Entry(membership).Property(current => current.RoleId).IsModified = true;
        database.Entry(role).Property(current => current.IsActive).IsModified = true;

        var actorId = scope!.UserId;
        database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
        {
            UserId = actorId,
            TenantId = companyId,
            EventType = "UserCompanyRoleAssigned",
            EntityType = "UserCompanyMembership",
            EntityId = $"{userId}:{companyId}",
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { userId, previousRoleId, roleId }),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "عضویت یا گروه دسترسی هم‌زمان توسط کاربر دیگری تغییر کرده است؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Lists the active members of one role of one company. Only minimal identity fields are
    /// returned (no e-mail, phone, password hash, other-company data); the role must belong to
    /// the company in the route, so a foreign role id yields 404.
    /// </summary>
    public static async Task<IResult> GetRoleMembersAsync(
        Guid companyId,
        Guid roleId,
        int? page,
        int? pageSize,
        string? search,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        CancellationToken cancellationToken)
    {
        var (failure, _) = await AuthorizeCompanyScopeAsync(
            database, companyAccess, principal, companyId, ViewRolesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return TypedResults.NotFound();
        if (!await database.CompanyRoles.AsNoTracking()
                .AnyAsync(role => role.Id == roleId && role.TenantId == companyId, cancellationToken))
            return TypedResults.NotFound();

        // Same contract as the admin user directory: trim, ignore whitespace-only, refuse a
        // term longer than 120 characters with 400 (never a silent full-list fallback).
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > 120)
            return TypedResults.BadRequest(new { error = "عبارت جستجو بیش از حد طولانی است." });

        var normalizedPage = Math.Clamp(page ?? 1, 1, MaxMemberPage);
        var normalizedPageSize = Math.Clamp(pageSize ?? 25, 1, 100);

        var query = database.UserCompanyMemberships.AsNoTracking()
            .Where(membership => membership.TenantId == companyId && membership.RoleId == roleId && membership.IsActive);
        if (search is not null)
        {
            // Server-side filter on the fields the member table actually shows (username and
            // names). The filter runs before Count/Skip/Take so paging stays correct.
            query = query.Join(database.Users.AsNoTracking(), membership => membership.UserId, user => user.Id,
                    (membership, user) => new { membership, user })
                .Where(row => (row.user.Username != null && row.user.Username.Contains(search)) ||
                    (row.user.FirstName != null && row.user.FirstName.Contains(search)) ||
                    (row.user.LastName != null && row.user.LastName.Contains(search)))
                .Select(row => row.membership);
        }
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Join(database.Users.AsNoTracking(), membership => membership.UserId, user => user.Id,
                (membership, user) => new { membership, user })
            .OrderBy(row => row.user.LastName)
            .ThenBy(row => row.user.FirstName)
            .ThenBy(row => row.user.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(row => new CompanyRoleMemberResponse(
                row.user.Id,
                row.user.Username,
                row.user.FirstName,
                row.user.LastName,
                row.user.IsActive,
                row.membership.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        SetNoStore(httpContext);
        var utcItems = items.Select(item => item with
        {
            MemberSinceUtc = DateTime.SpecifyKind(item.MemberSinceUtc, DateTimeKind.Utc),
        }).ToArray();
        return TypedResults.Ok(new CompanyRoleMemberListResponse(utcItems, totalCount, normalizedPage, normalizedPageSize));
    }

    /// <summary>
    /// Moves every active member of the source role to another active role of the SAME company
    /// in one atomic save, so a role with members can then be deactivated. It never creates or
    /// reactivates a membership (D-065) and writes one audit event per moved user. System roles are refused.
    /// </summary>
    public static async Task<IResult> TransferMembersAsync(
        Guid companyId,
        Guid roleId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        [FromBody] TransferCompanyRoleMembersRequest? request,
        [FromServices] IAntiforgery antiforgery,
        Data.DatabaseContext database,
        [FromServices] Infrastructure.Tenancy.CompanyAccessService companyAccess,
        [FromServices] Infrastructure.Tenancy.DelegationCeilingService delegationCeiling,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var (failure, scope) = await AuthorizeCompanyScopeAsync(
            database, companyAccess, principal, companyId, AssignRolesPermission, cancellationToken);
        if (failure is not null) return failure;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);
        if (!await IsActiveCompanyAsync(database, companyId, cancellationToken)) return TypedResults.NotFound();

        if (request is null || request.TargetRoleId == Guid.Empty || request.TargetRoleId == roleId)
            return TypedResults.BadRequest(new { error = "گروه مقصد نامعتبر است یا با گروه مبدأ یکی است." });

        var roles = await database.CompanyRoles
            .Include(role => role.RolePermissions)
            .Where(role => role.TenantId == companyId && (role.Id == roleId || role.Id == request.TargetRoleId))
            .ToListAsync(cancellationToken);
        var source = roles.FirstOrDefault(role => role.Id == roleId);
        var target = roles.FirstOrDefault(role => role.Id == request.TargetRoleId);
        if (source is null || target is null) return TypedResults.NotFound();
        if (!target.IsActive)
            return TypedResults.Conflict(new { error = "گروه مقصد غیرفعال است." });
        // System roles (e.g. company-admin) are immutable (see UpdateRoleAsync). A bulk move out of
        // or into one would silently strip or grant company-wide administration, so it fails closed.
        if (source.IsSystemRole || target.IsSystemRole)
            return TypedResults.Conflict(new { error = "انتقال گروهی از یا به گروه سیستمی مجاز نیست." });

        var memberships = await database.UserCompanyMemberships
            .Where(membership => membership.TenantId == companyId && membership.RoleId == roleId && membership.IsActive)
            .OrderBy(membership => membership.UserId)
            .Take(MaxTransferBatchSize + 1)
            .ToListAsync(cancellationToken);
        if (memberships.Count > MaxTransferBatchSize)
            return TypedResults.Conflict(new
            {
                error = $"تعداد اعضا از سقف {MaxTransferBatchSize} نفر در هر انتقال بیشتر است و هیچ عضوی منتقل نشد."
            });
        if (memberships.Count == 0)
        {
            SetNoStore(httpContext);
            return TypedResults.Ok(new TransferCompanyRoleMembersResponse(0));
        }

        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        // A company manager neither moves their own membership in bulk nor moves anybody
        // into a role whose permission set exceeds their freshly re-read ceiling (D-071).
        if (!scope!.IsInstallationAdministrator)
        {
            if (memberships.Any(membership => membership.UserId == scope.UserId))
                return ForbiddenSelfAssignment();
            var ceiling = await delegationCeiling.LoadTrackedCeilingAsync(scope.UserId, companyId, cancellationToken);
            if (ceiling is null) return TypedResults.NotFound();
            if (ceiling.Violates(target.RolePermissions.Select(rolePermission => rolePermission.PermissionId), out _))
                return ForbiddenBeyondCeiling();
            ceiling.ApplyRowVersionGuards(database);
        }

        // The role tokens protect the preconditions (source/target are still the roles
        // inspected above); membership tokens protect every row in the atomic batch.
        database.Entry(source).Property(current => current.IsActive).IsModified = true;
        database.Entry(target).Property(current => current.IsActive).IsModified = true;

        foreach (var membership in memberships)
        {
            membership.RoleId = target.Id;
            membership.Role = target;
        }

        // One small event per moved user (same event type as the single assignment), so the
        // audit stays inside the 4000-character DetailsJson column for any batch size up to the limit.
        var actorId = scope!.UserId;
        var occurredAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var correlationId = System.Diagnostics.Activity.Current?.Id;
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        foreach (var membership in memberships)
        {
            database.SecurityAuditEvents.Add(new Domain.SecurityAuditEvent
            {
                UserId = actorId,
                TenantId = companyId,
                EventType = "UserCompanyRoleAssigned",
                EntityType = "UserCompanyMembership",
                EntityId = $"{membership.UserId}:{companyId}",
                OccurredAtUtc = occurredAtUtc,
                CorrelationId = correlationId,
                IpAddress = ipAddress,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    userId = membership.UserId,
                    previousRoleId = roleId,
                    roleId = target.Id,
                    bulkTransfer = true,
                    sourceRoleWasActive = source.IsActive,
                }),
            });
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            SetNoStore(httpContext);
            return TypedResults.Conflict(new { error = "اعضای گروه هم‌زمان توسط کاربر دیگری تغییر کرده‌اند؛ اطلاعات را تازه‌سازی کنید." });
        }

        SetNoStore(httpContext);
        return TypedResults.Ok(new TransferCompanyRoleMembersResponse(memberships.Count));
    }

    /// <summary>
    /// Dual-mode authorization for one routed company (D-071). The installation administrator
    /// passes without a company membership; a company manager must prove an active membership
    /// plus the routed permission code. Anonymous callers get 401, callers without any
    /// relationship to the company get the same 404 as an unknown company (no disclosure),
    /// and members without the permission get an explicit 403.
    /// </summary>
    private static async Task<(IResult? Failure, CompanyScopeAccess? Scope)> AuthorizeCompanyScopeAsync(
        Data.DatabaseContext database,
        Infrastructure.Tenancy.CompanyAccessService companyAccess,
        ClaimsPrincipal principal,
        Guid companyId,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        var userId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal);
        if (userId is null)
            return (TypedResults.Unauthorized(), null);

        if (await UserAdministrationEndpoints.IsInstallationAdministratorAsync(userId.Value, database, cancellationToken))
            return (null, new CompanyScopeAccess(userId.Value, true));

        var access = await companyAccess.FindAccessibleCompanyAsync(userId.Value, companyId, cancellationToken);
        if (access is null)
            return (TypedResults.NotFound(), null);

        if (!await companyAccess.HasPermissionAsync(userId.Value, companyId, permissionCode, cancellationToken))
            return (TypedResults.Json(
                new { error = "شما مجاز به این عملیات در این شرکت نیستید." },
                statusCode: StatusCodes.Status403Forbidden), null);

        return (null, new CompanyScopeAccess(userId.Value, false));
    }

    private static IResult ForbiddenBeyondCeiling() =>
        TypedResults.Json(
            new { error = "شما مجاز به واگذاری این گروه یا مجوزها نیستید؛ سقف تفویض شما کمتر از این است." },
            statusCode: StatusCodes.Status403Forbidden);

    private static IResult ForbiddenSelfAssignment() =>
        TypedResults.Json(
            new { error = "تغییر نقش خودتان از این مسیر مجاز نیست." },
            statusCode: StatusCodes.Status403Forbidden);

    private sealed record CompanyScopeAccess(Guid UserId, bool IsInstallationAdministrator);

    private static async Task<bool> IsActiveCompanyAsync(
        Data.DatabaseContext database,
        Guid companyId,
        CancellationToken cancellationToken) =>
        companyId != Guid.Empty && await database.Companies.AsNoTracking()
            .AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken);

    private static async Task<IReadOnlyList<string>?> ResolvePermissionCodesAsync(
        Data.DatabaseContext database,
        string? templateCode,
        IReadOnlyCollection<string>? requestedCodes,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<string> selected;
        if (requestedCodes is not null)
            selected = requestedCodes;
        else if (!string.IsNullOrWhiteSpace(templateCode))
        {
            var codes = await database.PermissionDefinitions.AsNoTracking()
                .Select(permission => permission.Code).ToListAsync(cancellationToken);
            var template = BuildTemplates(codes).FirstOrDefault(item => item.Code == templateCode);
            if (template is null) return null;
            selected = template.PermissionCodes;
        }
        else
            selected = [];

        var normalized = selected.Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        if (normalized.Length != selected.Count) return null;

        var registered = await database.PermissionDefinitions.AsNoTracking()
            .Where(permission => normalized.Contains(permission.Code))
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken);
        return registered.Count == normalized.Length ? normalized : null;
    }

    private static IReadOnlyList<CompanyRoleTemplateResponse> BuildTemplates(IReadOnlyCollection<string> codes)
    {
        bool IsTrade(string code) => code.StartsWith("trade.", StringComparison.Ordinal);
        bool IsRead(string code) => code.EndsWith(".view", StringComparison.Ordinal) ||
                                    code.EndsWith(".export", StringComparison.Ordinal);
        bool IsWrite(string code) => code.EndsWith(".create", StringComparison.Ordinal) ||
                                     code.EndsWith(".update", StringComparison.Ordinal);
        bool IsApprove(string code) => code.EndsWith(".approve", StringComparison.Ordinal) ||
                                       code.EndsWith(".close", StringComparison.Ordinal);
        return
        [
            new("trade.viewer", "بازرگانی — مشاهده", codes.Where(code => IsTrade(code) &&
                (IsRead(code) || code == "trade.reports.company.view")).OrderBy(code => code).ToArray()),
            new("trade.operator", "بازرگانی — کارشناس عملیاتی", codes.Where(code => IsTrade(code) &&
                (IsRead(code) || IsWrite(code) || code == "trade.reports.company.view")).OrderBy(code => code).ToArray()),
            new("trade.approver", "بازرگانی — تأییدکننده", codes.Where(code => IsTrade(code) &&
                (IsRead(code) || IsApprove(code) || code == "trade.reports.company.view")).OrderBy(code => code).ToArray()),
            new("trade.manager", "بازرگانی — مدیر", codes.Where(code => IsTrade(code) &&
                code != "trade.reports.multicompany.view").OrderBy(code => code).ToArray()),
        ];
    }

    private static void SetNoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }
}

public sealed record CompanyRoleResponse(
    Guid Id, Guid CompanyId, string Code, string Name, bool IsSystemRole, bool IsActive,
    int PermissionCount, int ActiveMemberCount, IReadOnlyList<string> PermissionCodes);
public sealed record PermissionCatalogItemResponse(Guid Id, string Code, string Name, string? Description, string SubsystemCode);
public sealed record CompanyRoleTemplateResponse(string Code, string Name, IReadOnlyList<string> PermissionCodes);
public sealed record CreateCompanyRoleRequest(string? Code, string? Name, string? TemplateCode, IReadOnlyCollection<string>? PermissionCodes);
public sealed record UpdateCompanyRoleRequest(string? Name, bool IsActive, IReadOnlyCollection<string> PermissionCodes);
public sealed record CompanyRoleCreatedResponse(Guid Id);
public sealed record EffectivePermissionsResponse(
    Guid UserId,
    Guid CompanyId,
    string[] RoleNames,
    string[] PermissionCodes);

public sealed record CompanyRoleMemberResponse(
    Guid UserId, string? Username, string? FirstName, string? LastName, bool IsUserActive, DateTime MemberSinceUtc);
public sealed record CompanyRoleMemberListResponse(
    IReadOnlyList<CompanyRoleMemberResponse> Items, int TotalCount, int Page, int PageSize);
public sealed record TransferCompanyRoleMembersRequest(Guid TargetRoleId);
public sealed record TransferCompanyRoleMembersResponse(int MovedCount);
