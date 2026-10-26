using Faranou.Vendor.Host.Authentication;
using Faranou.Vendor.Host.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorPermissionPolicyProviderTests
{
    [Fact]
    public async Task KnownPermissionCreatesMfaAndPermissionPolicy()
    {
        var options = new AuthorizationOptions();
        VendorAuthorizationPolicies.Configure(options);
        var provider = new VendorPermissionPolicyProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync(
            $"{VendorPermissionPolicyProvider.PolicyPrefix}licenses.issue");

        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, requirement => requirement is VendorAdminAccessRequirement);
        Assert.Contains(policy.Requirements, requirement =>
            requirement is VendorPermissionRequirement permission &&
            permission.PermissionCode == "licenses.issue");
    }

    [Fact]
    public async Task UnknownPermissionDoesNotProduceAnAuthorizationPolicy()
    {
        var options = new AuthorizationOptions();
        VendorAuthorizationPolicies.Configure(options);
        var provider = new VendorPermissionPolicyProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync(
            $"{VendorPermissionPolicyProvider.PolicyPrefix}licenses.unlisted");

        Assert.Null(policy);
    }

    [Fact]
    public void CatalogIsVersionedAndContainsUniqueStableCodes()
    {
        Assert.Equal(1, VendorPermissionCatalog.Version);
        Assert.NotEmpty(VendorPermissionCatalog.All);
        Assert.Equal(
            VendorPermissionCatalog.All.Count,
            VendorPermissionCatalog.All.Select(permission => permission.Code)
                .Distinct(StringComparer.Ordinal).Count());
        Assert.All(VendorPermissionCatalog.All, permission => Assert.False(string.IsNullOrWhiteSpace(permission.Code)));
    }
}
