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
public static class CompanyRoleAdministrationEndpoints
{
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
        roles.MapPost("/{roleId:guid}/members/{userId:guid}", AssignMembershipAsync)
            .RequireRateLimiting(Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);

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
        CancellationToken cancellationToken)
    {
        var authorization = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null) return authorization;
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

    public static async Task<IResult> GetPermissionCatalogAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        Data.DatabaseContext database,
        CancellationToken cancellationToken)
    {
        var authorization = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null) return authorization;

        var registeredPermissions = await database.PermissionDefinitions.AsNoTracking()
            .OrderBy(permission => permission.Code)
            .Select(permission => new { permission.Id, permission.Code, permission.Name, permission.Description })
            .ToListAsync(cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var authorization = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null) return authorization;

        var codes = await database.PermissionDefinitions.AsNoTracking()
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken);
        var templates = BuildTemplates(codes);
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
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var authorization = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null) return authorization;
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

        var actorId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal)!.Value;
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

        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return TypedResults.Conflict(new { error = "کد گروه دسترسی در این شرکت تکراری است." }); }

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
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var authorization = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null) return authorization;
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

        database.CompanyRolePermissions.RemoveRange(role.RolePermissions);
        role.RolePermissions.Clear();
        foreach (var permissionId in permissionIds)
            role.RolePermissions.Add(new Domain.CompanyRolePermission { Role = role, PermissionId = permissionId });
        role.Name = name;
        role.IsActive = request.IsActive;

        var actorId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal)!.Value;
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

        await database.SaveChangesAsync(cancellationToken);
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
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var authorization = await UserAdministrationEndpoints.AuthorizeInstallationAdministratorAsync(principal, database, cancellationToken);
        if (authorization is not null) return authorization;
        if (!await antiforgery.IsRequestValidAsync(httpContext))
            return TypedResults.Problem(title: "درخواست نامعتبر است.", statusCode: StatusCodes.Status400BadRequest);

        var role = await database.CompanyRoles.FirstOrDefaultAsync(
            current => current.Id == roleId && current.TenantId == companyId && current.IsActive,
            cancellationToken);
        var company = await database.Companies.FirstOrDefaultAsync(
            current => current.Id == companyId && current.IsActive, cancellationToken);
        var user = await database.Users.FirstOrDefaultAsync(current => current.Id == userId && current.IsActive, cancellationToken);
        if (role is null || company is null || user is null) return TypedResults.NotFound();

        var membership = await database.UserCompanyMemberships.FirstOrDefaultAsync(
            current => current.UserId == userId && current.TenantId == companyId,
            cancellationToken);

        // A role assignment must never create or reactivate a cross-company membership.
        // New memberships require the destination-company approval workflow (D-065),
        // which is implemented separately from role grants. Until that workflow exists,
        // fail closed instead of making a pending request operationally active.
        if (membership is null || !membership.IsActive)
            return TypedResults.Conflict(new
            {
                error = "عضویت جدید یا غیرفعال باید ابتدا از مسیر درخواست و تأیید شرکت مقصد تعیین تکلیف شود."
            });

        var previousRoleId = membership.RoleId;
        membership.RoleId = roleId;
        membership.Role = role;

        var actorId = Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal)!.Value;
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

        await database.SaveChangesAsync(cancellationToken);
        SetNoStore(httpContext);
        return TypedResults.NoContent();
    }

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
