using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class LivenessEndpointTests
{
    [Fact]
    public async Task Live_endpoint_returns_success_without_diagnostic_details()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ConnectionStrings", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_path_returns_not_found()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/internal/configuration");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
