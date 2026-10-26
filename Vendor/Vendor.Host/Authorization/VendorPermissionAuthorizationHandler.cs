using System.Security.Claims;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Resolves role grants from the database on every authorization decision; it deliberately
/// does not trust permission claims cached in a potentially stale authentication cookie.
/// </summary>
public sealed class VendorPermissionAuthorizationHandler(
    UserManager<VendorAdminUser> users,
    RoleManager<IdentityRole<Guid>> roles)
    : AuthorizationHandler<VendorPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        VendorPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null || !user.IsEnabled)
        {
            return;
        }

        var roleNames = await users.GetRolesAsync(user);
        foreach (var roleName in roleNames)
        {
            var role = await roles.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            var claims = await roles.GetClaimsAsync(role);
            if (claims.Any(claim =>
                    string.Equals(claim.Type, VendorPermissionCatalog.ClaimType, StringComparison.Ordinal) &&
                    string.Equals(claim.Value, requirement.PermissionCode, StringComparison.Ordinal)))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
