using Faranou.Vendor.Host.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Shared baseline for endpoints that use default or fallback authorization.
/// Explicit RequireAuthorization() must not bypass the Vendor MFA/active-account gate.
/// </summary>
public static class VendorAuthorizationPolicies
{
    public static void Configure(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var baseline = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new VendorAdminAccessRequirement())
            .Build();

        options.DefaultPolicy = baseline;
        options.FallbackPolicy = baseline;
    }
}
