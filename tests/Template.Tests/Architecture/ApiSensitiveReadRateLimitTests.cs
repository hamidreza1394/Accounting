using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Template.Tests.Integration;
using Xunit;

namespace Template.Tests.Architecture;

public sealed class ApiSensitiveReadRateLimitTests : IClassFixture<FaranouApplicationFactory>
{
    private readonly FaranouApplicationFactory factory;
    public ApiSensitiveReadRateLimitTests(FaranouApplicationFactory factory) => this.factory = factory;

    [Fact]
    public void AdministrativeIdentityReadsRequireBothApiAndSensitivePolicies()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>().ToArray();
        var routes = new[]
        {
            "/api/v1/admin/users",
            "/api/v1/admin/users/{userId:guid}",
            "/api/v1/companies/{companyId:guid}/users",
            "/api/v1/admin/companies/{companyId:guid}/roles/{roleId:guid}/members",
        };

        foreach (var route in routes)
        {
            var endpoint = Assert.Single(endpoints.Where(candidate =>
                string.Equals(candidate.RoutePattern.RawText, route, StringComparison.OrdinalIgnoreCase) &&
                candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET", StringComparer.OrdinalIgnoreCase) == true));
            var policies = endpoint.Metadata.GetOrderedMetadata<EnableRateLimitingAttribute>();
            Assert.Contains(policies, policy => policy.PolicyName == Infrastructure.Security.RateLimitingPolicies.ApiPolicy);
            Assert.Contains(policies, policy => policy.PolicyName == Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        }
    }
}
