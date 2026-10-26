using Faranou.Vendor.Host.Authentication;
using Faranou.Vendor.Host.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorAuthorizationPolicyTests
{
    [Fact]
    public void DefaultAndFallbackPoliciesBothRequireAuthenticatedMfaVendorAdministrator()
    {
        var options = new AuthorizationOptions();

        VendorAuthorizationPolicies.Configure(options);

        Assert.NotNull(options.DefaultPolicy);
        Assert.NotNull(options.FallbackPolicy);
        AssertBaseline(options.DefaultPolicy!);
        AssertBaseline(options.FallbackPolicy!);
    }

    private static void AssertBaseline(AuthorizationPolicy policy)
    {
        Assert.Contains(policy.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
        Assert.Contains(policy.Requirements, requirement => requirement is VendorAdminAccessRequirement);
        Assert.Contains(policy.Requirements, requirement =>
            requirement is VendorPermissionRequirement permission &&
            permission.PermissionCode == VendorPermissionCatalog.PanelAccess);
    }
}
