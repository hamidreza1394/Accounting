using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Produces policies only for permission codes in the versioned allowlist. Unknown permission
/// names resolve to no policy and therefore fail closed.
/// </summary>
public sealed class VendorPermissionPolicyProvider : IAuthorizationPolicyProvider
{
    public const string PolicyPrefix = "VendorPermission:";
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    public VendorPermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
        {
            return _fallbackProvider.GetPolicyAsync(policyName);
        }

        var permissionCode = policyName[PolicyPrefix.Length..];
        if (!VendorPermissionCatalog.Contains(permissionCode))
        {
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(
                new Faranou.Vendor.Host.Authentication.VendorAdminAccessRequirement(),
                new VendorPermissionRequirement(permissionCode))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallbackProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallbackProvider.GetFallbackPolicyAsync();
}
