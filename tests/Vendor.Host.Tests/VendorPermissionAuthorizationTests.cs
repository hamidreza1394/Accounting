using Faranou.Vendor.Host.Authentication;
using Faranou.Vendor.Host.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task EveryVendorApiEndpointDeclaresExplicitAccessMetadata()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        var dataSource = factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>();
        var apiEndpoints = dataSource.Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        Assert.NotEmpty(apiEndpoints);

        var unclassified = apiEndpoints
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null &&
                !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();
        Assert.Empty(unclassified);

        var publicApiPaths = apiEndpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var approvedPublicPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/api/auth/antiforgery",
            "/api/auth/password",
            "/api/auth/totp/verify",
            "/api/auth/passkey/options",
            "/api/auth/passkey/sign-in",
            "/api/auth/logout"
        };

        Assert.Subset(approvedPublicPaths, publicApiPaths);
        Assert.Subset(publicApiPaths, approvedPublicPaths);
    }

    [Fact]
    public async Task EveryProtectedVendorApiEndpointRetainsActiveMfaAdministratorBaseline()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        var dataSource = factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>();
        var policyProvider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var protectedEndpoints = dataSource.Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(endpoint =>
                endpoint.RoutePattern.RawText?.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) == true &&
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .ToArray();

        Assert.NotEmpty(protectedEndpoints);
        foreach (var endpoint in protectedEndpoints)
        {
            var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            Assert.NotEmpty(authorizeData);

            var effectivePolicy = await AuthorizationPolicy.CombineAsync(policyProvider, authorizeData);
            Assert.NotNull(effectivePolicy);
            Assert.Contains(
                effectivePolicy!.Requirements,
                requirement => requirement is VendorAdminAccessRequirement);
        }
    }

    [Fact]
    public async Task EveryVendorAdminManagementEndpointRequiresAdministratorManagementPermission()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        var dataSource = factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>();
        var policyProvider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var adminEndpoints = dataSource.Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/admin/", StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        Assert.NotEmpty(adminEndpoints);
        foreach (var endpoint in adminEndpoints)
        {
            var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            Assert.NotEmpty(authorizeData);
            var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authorizeData);
            Assert.NotNull(policy);
            Assert.Contains(policy!.Requirements, requirement =>
                requirement is VendorPermissionRequirement permission &&
                permission.Permission == VendorPermissionCatalog.AdministratorsManage);
            Assert.Contains(policy.Requirements, requirement => requirement is VendorAdminAccessRequirement);
        }
    }

    [Fact]
    public async Task ResourcePolicyCombinesMfaPermissionAndResourceBoundaryRequirements()
    {
        var provider = new VendorPermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync(
            VendorResourceCatalog.PolicyFor("license", VendorPermissionCatalog.LicensesIssue));

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, requirement => requirement is VendorAdminAccessRequirement);
        Assert.Contains(policy.Requirements, requirement =>
            requirement is VendorPermissionRequirement permission &&
            permission.Permission == VendorPermissionCatalog.LicensesIssue);
        Assert.Contains(policy.Requirements, requirement =>
            requirement is VendorResourcePermissionRequirement resource &&
            resource.ResourceType == "license" &&
            resource.Permission == VendorPermissionCatalog.LicensesIssue);
    }

    [Fact]
    public void ResourceCatalogAllowsOnlyKnownResourceAndPermissionPairs()
    {
        Assert.True(VendorResourceCatalog.Supports("customer", VendorPermissionCatalog.CustomersRead));
        Assert.True(VendorResourceCatalog.Supports("license", VendorPermissionCatalog.LicensesIssue));
        Assert.False(VendorResourceCatalog.Supports("customer", VendorPermissionCatalog.LicensesIssue));
        Assert.False(VendorResourceCatalog.Supports("tenant", VendorPermissionCatalog.CustomersRead));
        Assert.False(VendorResourceCatalog.Supports("customer", "vendor.superuser"));
        Assert.False(VendorResourceCatalog.Supports(null, VendorPermissionCatalog.CustomersRead));
    }

    [Fact]
    public async Task MissingResourceAdapterDeniesAllResourceAccess()
    {
        var evaluator = new DenyAllVendorResourceAccessEvaluator();
        var result = await evaluator.HasAccessAsync(
            new System.Security.Claims.ClaimsPrincipal(),
            new VendorResourceDescriptor("customer", Guid.NewGuid()),
            VendorPermissionCatalog.CustomersRead,
            CancellationToken.None);

        Assert.False(result);
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
