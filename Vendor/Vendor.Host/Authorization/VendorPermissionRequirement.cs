using Faranou.Vendor.Host.Authentication;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Requires an explicitly catalogued permission assigned as a claim to one of the user's Identity roles.
/// The shared Vendor administrator/MFA requirement is composed into the policy separately.
/// </summary>
public sealed record VendorPermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class VendorPermissionAuthorizationHandler(
    UserManager<VendorAdminUser> users,
    RoleManager<IdentityRole<Guid>> roles)
    : AuthorizationHandler<VendorPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        VendorPermissionRequirement requirement)
    {
        if (!VendorPermissionCatalog.Contains(requirement.Permission) ||
            context.User.Identity?.IsAuthenticated != true)
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

            var roleClaims = await roles.GetClaimsAsync(role);
            if (roleClaims.Any(claim =>
                    string.Equals(claim.Type, VendorPermissionCatalog.ClaimType, StringComparison.Ordinal) &&
                    string.Equals(claim.Value, requirement.Permission, StringComparison.Ordinal)))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}

/// <summary>
/// Resolves named permission policies. Unknown permission identifiers still produce a policy,
/// but the handler deliberately refuses to authorize them (fail closed).
/// </summary>
public sealed class VendorPermissionPolicyProvider(
    Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(VendorResourceCatalog.PolicyPrefix, StringComparison.Ordinal))
        {
            var resourcePolicyValue = policyName[VendorResourceCatalog.PolicyPrefix.Length..];
            var separator = resourcePolicyValue.IndexOf(':');
            var resourceType = separator > 0 ? resourcePolicyValue[..separator] : string.Empty;
            var permission = separator > 0 && separator < resourcePolicyValue.Length - 1
                ? resourcePolicyValue[(separator + 1)..]
                : string.Empty;

            var resourcePolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(
                    new VendorAdminAccessRequirement(),
                    new VendorPermissionRequirement(permission),
                    new VendorResourcePermissionRequirement(resourceType, permission))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(resourcePolicy);
        }

        if (!policyName.StartsWith(VendorPermissionCatalog.PolicyPrefix, StringComparison.Ordinal))
        {
            return base.GetPolicyAsync(policyName);
        }

        var standardPermission = policyName[VendorPermissionCatalog.PolicyPrefix.Length..];
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(
                new VendorAdminAccessRequirement(),
                new VendorPermissionRequirement(standardPermission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
