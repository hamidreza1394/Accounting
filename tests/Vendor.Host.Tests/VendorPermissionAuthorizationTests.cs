using Faranou.Vendor.Host.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorPermissionAuthorizationTests
{
    [Fact]
    public void CatalogHasUniqueVersionedPermissionIdentifiers()
    {
        Assert.True(VendorPermissionCatalog.Version > 0);
        Assert.NotEmpty(VendorPermissionCatalog.All);
        Assert.Equal(
            VendorPermissionCatalog.All.Count,
            VendorPermissionCatalog.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(VendorPermissionCatalog.All, permission =>
        {
            Assert.StartsWith("vendor.", permission, StringComparison.Ordinal);
            Assert.True(VendorPermissionCatalog.Contains(permission));
        });
    }

    [Fact]
    public void UnknownPermissionIsNotPartOfTheCatalog()
    {
        Assert.False(VendorPermissionCatalog.Contains("vendor.superuser"));
        Assert.False(VendorPermissionCatalog.Contains("Vendor.Licenses.Issue"));
    }

    [Fact]
    public async Task NamedPermissionPolicyRequiresMfaAdministratorAndPermission()
    {
        var options = new AuthorizationOptions();
        VendorAuthorizationPolicies.Configure(options);
        var provider = new VendorPermissionPolicyProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync(
            VendorPermissionCatalog.PolicyFor(VendorPermissionCatalog.LicensesIssue));

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
        Assert.Contains(policy.Requirements, requirement => requirement is VendorAdminAccessRequirement);
        Assert.Contains(
            policy.Requirements,
            requirement => requirement is VendorPermissionRequirement permission &&
                           permission.Permission == VendorPermissionCatalog.LicensesIssue);
    }

    [Fact]
    public async Task UnknownNamedPermissionStillCreatesDenyByDefaultRequirement()
    {
        var provider = new VendorPermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync(VendorPermissionCatalog.PolicyFor("vendor.unregistered.permission"));

        Assert.NotNull(policy);
        Assert.Contains(
            policy!.Requirements,
            requirement => requirement is VendorPermissionRequirement permission &&
                           !VendorPermissionCatalog.Contains(permission.Permission));
    }

    [Fact]
    public async Task NonPermissionPoliciesDelegateToDefaultProvider()
    {
        var options = new AuthorizationOptions();
        options.AddPolicy("ExistingCustomPolicy", policy => policy.RequireClaim("scope", "test"));
        var provider = new VendorPermissionPolicyProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync("ExistingCustomPolicy");

        Assert.NotNull(policy);
        Assert.Contains(
            policy!.Requirements,
            requirement => requirement is ClaimsAuthorizationRequirement claims &&
                           claims.ClaimType == "scope");
    }
}
