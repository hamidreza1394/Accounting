using System.Security.Claims;
using System.Text.RegularExpressions;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>Administrative role management. Privileged permission assignment is intentionally bootstrap-only.</summary>
public static partial class VendorRoleManagementEndpoints
{
    private const string BootstrapSuperAdminRole = "Vendor.SuperAdmin";
    private const string PermissionClaimType = VendorPermissionCatalog.ClaimType;

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9._-]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex RoleNameRegex();

    public static IEndpointRouteBuilder MapVendorRoleManagement(this IEndpointRouteBuilder endpoints)
    {
        var roles = endpoints.MapGroup("/api/admin/roles")
            .RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.AdministratorsManage));

        roles.MapGet("/", async (
            RoleManager<IdentityRole<Guid>> roleManager,
            CancellationToken cancellationToken) =>
        {
            var result = new List<RoleResponse>();
            foreach (var role in await roleManager.Roles.OrderBy(role => role.Name).ToListAsync(cancellationToken))
            {
                var permissions = (await roleManager.GetClaimsAsync(role))
                    .Where(claim => claim.Type == PermissionClaimType)
                    .Select(claim => claim.Value)
                    .Where(VendorPermissionCatalog.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                result.Add(new RoleResponse(role.Id, role.Name ?? string.Empty, permissions));
            }

            return Results.Ok(result);
        });

        roles.MapGet("/{roleId:guid}", async (
            Guid roleId,
            RoleManager<IdentityRole<Guid>> roleManager) =>
        {
            var role = await roleManager.FindByIdAsync(roleId.ToString("D"));
            if (role is null)
            {
                return Results.NotFound();
            }

            var permissions = (await roleManager.GetClaimsAsync(role))
                .Where(claim => claim.Type == PermissionClaimType)
                .Select(claim => claim.Value)
                .Where(VendorPermissionCatalog.Contains)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return Results.Ok(new RoleResponse(role.Id, role.Name ?? string.Empty, permissions));
        });

        roles.MapPost("/", async (
            CreateRoleRequest request,
            HttpContext context,
            VendorDbContext db,
            RoleManager<IdentityRole<Guid>> roleManager,
            UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit,
            CancellationToken cancellationToken) =>
        {
            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) ||
                !RoleNameRegex().IsMatch(name) ||
                string.Equals(name, BootstrapSuperAdminRole, StringComparison.OrdinalIgnoreCase) ||
                !TryNormalizePermissions(request.Permissions, out var permissions))
            {
                return Results.BadRequest(new { error = "invalid_role_definition" });
            }

            if (permissions.Contains(VendorPermissionCatalog.AdministratorsManage, StringComparer.Ordinal))
            {
                return Results.BadRequest(new { error = "privileged_permission_is_bootstrap_only" });
            }

            if (await roleManager.FindByNameAsync(name) is not null)
            {
                return Results.Conflict(new { error = "role_already_exists" });
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = name };
            var create = await roleManager.CreateAsync(role);
            if (!create.Succeeded)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Results.Conflict(new { error = "role_creation_failed" });
            }

            foreach (var permission in permissions)
            {
                var addClaim = await roleManager.AddClaimAsync(role, new Claim(PermissionClaimType, permission));
                if (!addClaim.Succeeded)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return Results.Conflict(new { error = "role_permission_assignment_failed" });
                }
            }

            var actor = await users.GetUserAsync(context.User);
            if (actor is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Results.Forbid();
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id,
                "vendor.rbac.role-created",
                nameof(IdentityRole<Guid>),
                role.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Created($"/api/admin/roles/{role.Id:D}", new RoleResponse(role.Id, role.Name!, permissions));
        });

        roles.MapPut("/{roleId:guid}/permissions", async (
            Guid roleId,
            UpdateRolePermissionsRequest request,
            HttpContext context,
            VendorDbContext db,
            RoleManager<IdentityRole<Guid>> roleManager,
            UserManager<VendorAdminUser> users,
            IVendorAuditWriter audit,
            CancellationToken cancellationToken) =>
        {
            if (!TryNormalizePermissions(request.Permissions, out var permissions))
            {
                return Results.BadRequest(new { error = "invalid_permission_set" });
            }

            if (permissions.Contains(VendorPermissionCatalog.AdministratorsManage, StringComparer.Ordinal))
            {
                return Results.BadRequest(new { error = "privileged_permission_is_bootstrap_only" });
            }

            var role = await roleManager.FindByIdAsync(roleId.ToString("D"));
            if (role is null)
            {
                return Results.NotFound();
            }

            if (string.Equals(role.Name, BootstrapSuperAdminRole, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Conflict(new { error = "bootstrap_super_admin_role_is_immutable" });
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var existing = (await roleManager.GetClaimsAsync(role))
                .Where(claim => claim.Type == PermissionClaimType)
                .ToArray();
            if (existing.Length > 0)
            {
                var remove = await roleManager.RemoveClaimsAsync(role, existing);
                if (!remove.Succeeded)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return Results.Conflict(new { error = "role_permission_update_failed" });
                }
            }

            foreach (var permission in permissions)
            {
                var add = await roleManager.AddClaimAsync(role, new Claim(PermissionClaimType, permission));
                if (!add.Succeeded)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return Results.Conflict(new { error = "role_permission_update_failed" });
                }
            }

            var actor = await users.GetUserAsync(context.User);
            if (actor is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Results.Forbid();
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id,
                "vendor.rbac.role-permissions-updated",
                nameof(IdentityRole<Guid>),
                role.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Ok(new RoleResponse(role.Id, role.Name ?? string.Empty, permissions));
        });

        var administrators = endpoints.MapGroup("/api/admin/administrators")
            .RequireAuthorization(VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.AdministratorsManage));

        administrators.MapPost("/{userId:guid}/roles/{roleId:guid}", async (
            Guid userId,
            Guid roleId,
            HttpContext context,
            VendorDbContext db,
            UserManager<VendorAdminUser> users,
            RoleManager<IdentityRole<Guid>> roleManager,
            IVendorAuditWriter audit,
            CancellationToken cancellationToken) =>
        {
            var user = await users.FindByIdAsync(userId.ToString("D"));
            var role = await roleManager.FindByIdAsync(roleId.ToString("D"));
            if (user is null || role is null)
            {
                return Results.NotFound();
            }

            if (!user.IsEnabled || string.Equals(role.Name, BootstrapSuperAdminRole, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Conflict(new { error = "administrator_or_role_not_assignable" });
            }

            if (await users.IsInRoleAsync(user, role.Name!))
            {
                return Results.NoContent();
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var assignment = await users.AddToRoleAsync(user, role.Name!);
            if (!assignment.Succeeded)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Results.Conflict(new { error = "role_assignment_failed" });
            }

            var actor = await users.GetUserAsync(context.User);
            if (actor is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Results.Forbid();
            }

            await audit.AppendAsync(new VendorAuditWriteRequest(
                actor.Id,
                "vendor.rbac.role-assigned",
                nameof(VendorAdminUser),
                user.Id.ToString("D"),
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }

    private static bool TryNormalizePermissions(IReadOnlyCollection<string>? requested, out string[] permissions)
    {
        permissions = [];
        if (requested is null || requested.Count > VendorPermissionCatalog.All.Count ||
            requested.Any(permission => !VendorPermissionCatalog.Contains(permission)) ||
            requested.Distinct(StringComparer.Ordinal).Count() != requested.Count)
        {
            return false;
        }

        permissions = requested.Order(StringComparer.Ordinal).ToArray();
        return true;
    }

    public sealed record CreateRoleRequest(string? Name, IReadOnlyCollection<string>? Permissions);
    public sealed record UpdateRolePermissionsRequest(IReadOnlyCollection<string>? Permissions);
    public sealed record RoleResponse(Guid Id, string Name, IReadOnlyCollection<string> Permissions);
}
