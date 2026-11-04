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
            var matches = endpoints.Where(candidate =>
                string.Equals(candidate.RoutePattern.RawText?.TrimEnd('/'), route.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) &&
                candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET", StringComparer.OrdinalIgnoreCase) == true)
                .ToArray();
            Assert.True(matches.Length == 1,
                $"Expected one GET route '{route}', found {matches.Length}. Mapped GET routes: " +
                string.Join(", ", endpoints.Where(candidate =>
                    candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET", StringComparer.OrdinalIgnoreCase) == true)
                    .Select(candidate => candidate.RoutePattern.RawText)));
            var endpoint = matches[0];
            var policies = endpoint.Metadata.GetOrderedMetadata<EnableRateLimitingAttribute>();
            Assert.Contains(policies, policy => policy.PolicyName == Infrastructure.Security.RateLimitingPolicies.ApiPolicy);
            Assert.Contains(policies, policy => policy.PolicyName == Infrastructure.Security.RateLimitingPolicies.ApiSensitivePolicy);
        }
    }
}
